using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrispShot.Services;

public sealed class WindowCaptureService
{
    public bool IsSupported() => WgcCapture.IsSupported();

    public async Task<CapturedWindow?> CaptureAsync(nint windowHandle, CancellationToken cancellationToken)
    {
        if (windowHandle == 0) return null;

        var hwnd = new HWND(windowHandle);

        try
        {
            if (PInvoke.IsIconic(hwnd))
            {
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
            }
        }
        catch (Exception) { }

        try
        {
            var (pixels, width, height) = await WgcCapture.CaptureAsync(hwnd, cancellationToken).ConfigureAwait(false);
            if (width <= 0 || height <= 0) return null;
            return new CapturedWindow(pixels, width, height);
        }
        catch (Exception) { return null; }
    }
}

public sealed record CapturedWindow(byte[] BgraPixels, int Width, int Height);
