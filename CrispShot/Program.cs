using CrispShot.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Linq;
using System.Threading;

namespace CrispShot;

public static class Program
{
    private const string SingleInstanceKey = "CrispShot_SingleInstance";

    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var currentAppActivationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (TryRedirectToExistingRegisteredInstance(currentAppActivationArguments)) return;

        var administratorRunService = new AdministratorRunService();
        if (administratorRunService.ShouldLaunchAsAdministrator(currentAppActivationArguments) && administratorRunService.TryLaunchAsAdministratorAsync().GetAwaiter().GetResult()) return;

        if (TryRegisterOrRedirectToMainInstance(currentAppActivationArguments)) return;

        Application.Start(applicationInitializationCallbackParameters =>
        {
            _ = applicationInitializationCallbackParameters;
            var dispatcherQueueSynchronizationContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(dispatcherQueueSynchronizationContext);
            _ = new App();
        });
    }

    public static void HandleRedirectedActivation(AppActivationArguments appActivationArguments)
    {
        if (Application.Current is not App currentApplication) return;
        if (currentApplication._trayHostWindow is not null)
        {
            if (currentApplication._trayHostWindow.DispatcherQueue.TryEnqueue(() => currentApplication.OnRedirectedActivation(appActivationArguments)))
            {
                return;
            }
        }

        currentApplication.OnRedirectedActivation(appActivationArguments);
    }

    private static bool TryRedirectToExistingRegisteredInstance(AppActivationArguments currentAppActivationArguments)
    {
        var existingMainInstance = AppInstance.GetInstances().FirstOrDefault(appInstance => string.Equals(appInstance.Key, SingleInstanceKey, StringComparison.Ordinal));
        if (existingMainInstance is null) return false;

        existingMainInstance.RedirectActivationToAsync(currentAppActivationArguments).AsTask().GetAwaiter().GetResult();
        return true;
    }

    private static bool TryRegisterOrRedirectToMainInstance(AppActivationArguments currentAppActivationArguments)
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (mainInstance.IsCurrent)
        {
            mainInstance.Activated += OnMainInstanceActivated;
            return false;
        }

        mainInstance.RedirectActivationToAsync(currentAppActivationArguments).AsTask().GetAwaiter().GetResult();
        return true;
    }

    private static void OnMainInstanceActivated(object? sender, AppActivationArguments appActivationArguments) => HandleRedirectedActivation(appActivationArguments);
}