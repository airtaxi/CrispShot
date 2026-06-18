using CrispShot.Enums;
using CrispShot.Models;
using Microsoft.UI.Dispatching;

namespace CrispShot.Services;

public sealed class CapturePipelineService(WindowCaptureService windowCaptureService, ShadowRendererService shadowRendererService, ClipboardService clipboardService, SettingsService settingsService, LocalizationService localizationService, DispatcherQueue dispatcherQueue)
{
    private readonly SemaphoreSlim _pipelineGate = new(1, 1);

    public event EventHandler<CaptureResult>? CaptureCompleted;

    public async Task ProcessCaptureAsync(nint windowHandle)
    {
        if (!windowCaptureService.IsSupported()) return;
        if (!await _pipelineGate.WaitAsync(0)) return;

        try
        {
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var capture = await windowCaptureService.CaptureAsync(windowHandle, cancellationSource.Token);
            if (capture is null)
            {
                CaptureCompleted?.Invoke(this, CaptureResult.Failed(localizationService.GetString("Capture.WgcFailed")));
                return;
            }

            var alphaKind = AlphaChannelAnalyzer.Analyze(capture.BgraPixels, capture.Width, capture.Height);
            if (alphaKind == AlphaChannelKind.Absent && !settingsService.Current.ProcessWhenNoAlpha)
            {
                CaptureCompleted?.Invoke(this, CaptureResult.Failed(localizationService.GetString("Capture.AlphaAbsent")));
                return;
            }

            var settings = settingsService.Current;
            var shadowOptions = ShadowRenderOptions.FromIntensity(settings.ShadowIntensity);
            var pngBytes = shadowRendererService.Render(capture, shadowOptions);

            await SetClipboardOnDispatcherAsync(pngBytes, cancellationSource.Token);
            CaptureCompleted?.Invoke(this, CaptureResult.Succeeded(capture.Width, capture.Height, pngBytes.Length, alphaKind));
        }
        catch (Exception exception) { CaptureCompleted?.Invoke(this, CaptureResult.Failed(exception.Message)); }
        finally { _pipelineGate.Release(); }
    }

    private Task SetClipboardOnDispatcherAsync(byte[] pngBytes, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await clipboardService.SetPngAsync(pngBytes);
                taskCompletionSource.SetResult();
            }
            catch (Exception exception) { taskCompletionSource.SetException(exception); }
        })) taskCompletionSource.SetException(new InvalidOperationException("UI dispatcher is not available."));

        return taskCompletionSource.Task.WaitAsync(cancellationToken);
    }
}

public sealed record CaptureResult(bool IsSuccessful, int Width, int Height, int ByteCount, AlphaChannelKind AlphaKind, string? FailureReason)
{
    public static CaptureResult Succeeded(int width, int height, int byteCount, AlphaChannelKind alphaKind) =>
        new(true, width, height, byteCount, alphaKind, null);

    public static CaptureResult Failed(string reason) =>
        new(false, 0, 0, 0, AlphaChannelKind.Invalid, reason);
}
