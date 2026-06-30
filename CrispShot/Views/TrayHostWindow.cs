using CrispShot.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace CrispShot.Views;

public sealed class TrayHostWindow : Window
{
    private SettingsService? _settingsService;
    private LocalizationService? _localizationService;
    private StartupTaskService? _startupTaskService;
    private AdministratorRunService? _administratorRunService;
    private WindowCaptureService? _windowCaptureService;
    private ShadowRendererService? _shadowRendererService;
    private ClipboardService? _clipboardService;
    private KeyboardHookService? _keyboardHookService;
    private CapturePipelineService? _capturePipelineService;
    private TrayIconManager? _trayIconManager;
    private bool _initialized;

    public TrayHostWindow()
    {
        Title = "CrispShot";
        Content = new Grid();
        AppWindow.Title = "CrispShot";
        AppWindow.SetIcon(AssetPathResolver.IconFilePath);
        AppWindow.IsShownInSwitchers = false;
        Closed += OnTrayHostWindowClosed;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        HideWindow();

        _settingsService = new SettingsService();
        _localizationService = new LocalizationService(_settingsService);
        _localizationService.Initialize();
        _administratorRunService = new AdministratorRunService();
        _startupTaskService = new StartupTaskService(_settingsService, _administratorRunService);
        _windowCaptureService = new WindowCaptureService();
        _shadowRendererService = new ShadowRendererService();
        _clipboardService = new ClipboardService();
        _capturePipelineService = new CapturePipelineService(_windowCaptureService, _shadowRendererService, _clipboardService, _settingsService, _localizationService, DispatcherQueue);
        _capturePipelineService.CaptureCompleted += OnCaptureCompleted;

        _keyboardHookService = new KeyboardHookService();
        _keyboardHookService.AltPrintScreenPressed += OnAltPrintScreenPressed;
        _keyboardHookService.Initialize();

        _trayIconManager = new TrayIconManager(_settingsService, _startupTaskService, _administratorRunService, _localizationService);
        _trayIconManager.ExitRequested += OnTrayIconManagerExitRequested;
        _trayIconManager.Initialize();

        await _startupTaskService.SynchronizeStoredPreferenceAsync();

        _initialized = true;
    }

    private void OnTrayHostWindowClosed(object? _, WindowEventArgs __) => DisposeServices();

    private void OnTrayIconManagerExitRequested(object? _, EventArgs __)
    {
        Close();
        Application.Current.Exit();
    }

    private void HideWindow()
    {
        AppWindow.IsShownInSwitchers = false;
        var windowHandle = new HWND(WindowNative.GetWindowHandle(this));
        PInvoke.ShowWindow(windowHandle, SHOW_WINDOW_CMD.SW_HIDE);
    }

    private void OnAltPrintScreenPressed(object? _, nint windowHandle)
    {
        if (windowHandle == 0) return;
        var capturePipelineService = _capturePipelineService;
        if (capturePipelineService is null) return;

        DispatcherQueue.TryEnqueue(() => _ = capturePipelineService.ProcessCaptureAsync(windowHandle));
    }

    private void OnCaptureCompleted(object? _, CaptureResult result)
    {
        if (!result.IsSuccessful) System.Diagnostics.Debug.WriteLine($"CrispShot capture skipped: {result.FailureReason}");
        else System.Diagnostics.Debug.WriteLine($"CrispShot capture OK: {result.Width}x{result.Height}, {result.ByteCount / 1024}KB (alpha={result.AlphaKind})");
    }

    private void DisposeServices()
    {
        if (_trayIconManager is not null)
        {
            _trayIconManager.ExitRequested -= OnTrayIconManagerExitRequested;
            _trayIconManager.Dispose();
            _trayIconManager = null;
        }

        _keyboardHookService?.AltPrintScreenPressed -= OnAltPrintScreenPressed;
        _keyboardHookService?.Dispose();
        _keyboardHookService = null;

        _capturePipelineService?.CaptureCompleted -= OnCaptureCompleted;
        _capturePipelineService = null;

        _windowCaptureService = null;
        _shadowRendererService = null;
        _clipboardService = null;
        _administratorRunService = null;
        _startupTaskService = null;
        _localizationService = null;
        _settingsService = null;
    }
}
