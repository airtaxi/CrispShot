using Windows.ApplicationModel;

namespace CrispShot.Services;

public sealed class StartupTaskService(SettingsService settingsService)
{
    private const string StartupTaskIdentifier = "CrispShotStartupTask";

    public async Task SynchronizeStoredPreferenceAsync()
    {
        if (settingsService.Current.StartWithWindows) await SetStartWithWindowsEnabledAsync(true);
        else await SetStartWithWindowsEnabledAsync(false);
    }

    public async Task<bool> SetStartWithWindowsEnabledAsync(bool isEnabled)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskIdentifier);
            if (!isEnabled)
            {
                startupTask.Disable();
                settingsService.SetStartWithWindows(false);
                return true;
            }

            if (startupTask.State == StartupTaskState.Enabled)
            {
                settingsService.SetStartWithWindows(true);
                return true;
            }

            var startupTaskState = await startupTask.RequestEnableAsync();
            var wasEnabled = startupTaskState == StartupTaskState.Enabled;
            settingsService.SetStartWithWindows(wasEnabled);
            return wasEnabled;
        }
        catch (Exception)
        {
            settingsService.SetStartWithWindows(false);
            return false;
        }
    }
}
