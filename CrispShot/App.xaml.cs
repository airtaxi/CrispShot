using CrispShot.Views;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CrispShot;

public partial class App : Application
{
    internal TrayHostWindow? _trayHostWindow;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs launchActivatedEventArgs)
    {
        _ = launchActivatedEventArgs;
        await InitializeApplicationAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    internal void OnRedirectedActivation(AppActivationArguments appActivationArguments) => _ = InitializeApplicationAsync(appActivationArguments);

    private async Task InitializeApplicationAsync(AppActivationArguments appActivationArguments)
    {
        _ = appActivationArguments;
        _trayHostWindow = new TrayHostWindow();
        _trayHostWindow.Activate();
        await _trayHostWindow.InitializeAsync();
    }
}