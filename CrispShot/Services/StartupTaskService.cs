using CrispShot.Enums;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Security.Principal;
using Windows.ApplicationModel;
using Task = System.Threading.Tasks.Task;

namespace CrispShot.Services;

public sealed class StartupTaskService
{
    private const string StartupTaskIdentifier = "CrispShotStartupTask";
    private const string StartupScheduledTaskName = "CrispShot.Startup";
    private const string ActivationProtocolName = "crispshot";
    private const string StartupActivationToken = "startup";

    private readonly SettingsService _settingsService;
    private readonly AdministratorRunService _administratorRunService;

    public StartupTaskService(SettingsService settingsService, AdministratorRunService administratorRunService)
    {
        _settingsService = settingsService;
        _administratorRunService = administratorRunService;
        _administratorRunService.RegistrationChanged += OnAdministratorRegistrationChanged;
    }

    public async Task SynchronizeStoredPreferenceAsync()
    {
        try { await ApplyStartupRegistrationAsync(_settingsService.Current.StartWithWindows); }
        catch (Exception exception) { Debug.WriteLine($"CrispShot startup task synchronization failed: {exception.Message}"); }
    }

    public async Task<bool> SetStartWithWindowsEnabledAsync(bool isEnabled)
    {
        try
        {
            var wasApplied = await ApplyStartupRegistrationAsync(isEnabled);
            _settingsService.SetStartWithWindows(wasApplied && isEnabled);
            return wasApplied;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"CrispShot startup task update failed: {exception.Message}");
            _settingsService.SetStartWithWindows(false);
            return false;
        }
    }

    private async void OnAdministratorRegistrationChanged(object? _, EventArgs __) => await SynchronizeStoredPreferenceAsync();

    private async Task<bool> ApplyStartupRegistrationAsync(bool isEnabled)
    {
        var isAdministratorRegistered = _administratorRunService.GetRegistrationState() == AdministratorRunRegistrationState.Registered;

        if (!isEnabled)
        {
            DeleteStartupScheduledTask();
            await DisableStoreStartupTaskAsync();
            return true;
        }

        if (isAdministratorRegistered)
        {
            if (!IsStartupScheduledTaskRegistered()) RegisterStartupScheduledTask();
            await DisableStoreStartupTaskAsync();
            return true;
        }

        DeleteStartupScheduledTask();
        return await EnableStoreStartupTaskAsync();
    }

    private static async Task<StartupTask> GetStoreStartupTaskAsync() => await StartupTask.GetAsync(StartupTaskIdentifier);

    private static async Task DisableStoreStartupTaskAsync()
    {
        var storeStartupTask = await GetStoreStartupTaskAsync();
        if (storeStartupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy) storeStartupTask.Disable();
    }

    private static async Task<bool> EnableStoreStartupTaskAsync()
    {
        var storeStartupTask = await GetStoreStartupTaskAsync();
        if (storeStartupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy) return true;

        var startupTaskState = await storeStartupTask.RequestEnableAsync();
        return startupTaskState == StartupTaskState.Enabled;
    }

    private static bool IsStartupScheduledTaskRegistered()
    {
        using var taskService = new TaskService();
        return taskService.GetTask(StartupScheduledTaskName) is not null;
    }

    private static void RegisterStartupScheduledTask()
    {
        using var taskService = new TaskService();
        var taskDefinition = taskService.NewTask();
        taskDefinition.RegistrationInfo.Description = "Launch CrispShot when the current user signs in.";
        taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;
        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
        taskDefinition.Settings.AllowDemandStart = true;
        taskDefinition.Settings.DisallowStartIfOnBatteries = false;
        taskDefinition.Settings.StopIfGoingOnBatteries = false;
        taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        taskDefinition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
        taskDefinition.Settings.StartWhenAvailable = true;
        taskDefinition.Triggers.Add(new LogonTrigger { UserId = GetCurrentUserId() });

        var commandProcessorPath = GetCommandProcessorPath();
        taskDefinition.Actions.Add(new ExecAction(commandProcessorPath, CreateCommandProcessorArguments(), Path.GetDirectoryName(commandProcessorPath)));
        taskService.RootFolder.RegisterTaskDefinition(StartupScheduledTaskName, taskDefinition, TaskCreation.CreateOrUpdate, null, null, TaskLogonType.InteractiveToken);
    }

    private static void DeleteStartupScheduledTask()
    {
        using var taskService = new TaskService();
        if (taskService.GetTask(StartupScheduledTaskName) is not null) taskService.RootFolder.DeleteTask(StartupScheduledTaskName, exceptionOnNotExists: false);
    }

    private static string GetCurrentUserId() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("The current user security identifier could not be resolved.");

    private static string CreateCommandProcessorArguments() => $"/c start \"\" \"{ActivationProtocolName}://{StartupActivationToken}\"";

    private static string GetCommandProcessorPath() => Path.Combine(Environment.SystemDirectory, "cmd.exe");
}
