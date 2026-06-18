using CrispShot.Services;
using CrispShot.Views;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;

namespace CrispShot;

public partial class App : Application
{
    private TrayHostWindow? _trayHostWindow;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var currentAppActivationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var administratorRunService = new AdministratorRunService();
        if (administratorRunService.ShouldLaunchAsAdministrator(currentAppActivationArguments) && await administratorRunService.TryLaunchAsAdministratorAsync())
        {
            Process.GetCurrentProcess().Kill();
            return;
        }

        var currentInstance = AppInstance.FindOrRegisterForKey("CrispShot");
        if (!currentInstance.IsCurrent)
        {
            await currentInstance.RedirectActivationToAsync(currentAppActivationArguments);
            Process.GetCurrentProcess().Kill();
            return;
        }

        _trayHostWindow = new TrayHostWindow();
        _trayHostWindow.Activate();
        await _trayHostWindow.InitializeAsync();
    }
}
