using Microsoft.Windows.AppLifecycle;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Security.Principal;
using Windows.ApplicationModel.Activation;
using CrispShot.Enums;

namespace CrispShot.Services;

public sealed class AdministratorRunService
{
    public const string AdministratorActivationToken = "administrator-task";

    private const string ActivationProtocolName = "crispshot";
    private const string AdministratorScheduledTaskName = "CrispShot.Administrator";

    public bool IsCurrentProcessElevated
    {
        get
        {
            using var windowsIdentity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(windowsIdentity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public AdministratorRunRegistrationState GetRegistrationState()
    {
        try
        {
            using var taskService = new TaskService();
            return taskService.GetTask(AdministratorScheduledTaskName) is null ? AdministratorRunRegistrationState.NotRegistered : AdministratorRunRegistrationState.Registered;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"CrispShot administrator task query failed: {exception.Message}");
            return AdministratorRunRegistrationState.Unknown;
        }
    }

    public System.Threading.Tasks.Task<bool> SetEnabledAsync(bool isEnabled) => System.Threading.Tasks.Task.Run(() =>
    {
        if (!IsCurrentProcessElevated) return false;

        try
        {
            if (isEnabled) RegisterTask();
            else DeleteTask();
            return true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"CrispShot administrator task update failed: {exception.Message}");
            return false;
        }
    });

    public bool ShouldLaunchAsAdministrator(AppActivationArguments appActivationArguments) => !IsCurrentProcessElevated && !IsAdministratorActivation(appActivationArguments) && GetRegistrationState() == AdministratorRunRegistrationState.Registered;

    public Task<bool> TryLaunchAsAdministratorAsync() => System.Threading.Tasks.Task.Run(() =>
    {
        try
        {
            using var taskService = new TaskService();
            var task = taskService.GetTask(AdministratorScheduledTaskName);
            if (task is null) return false;

            task.Run();
            return true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"CrispShot administrator task launch failed: {exception.Message}");
            return false;
        }
    });

    private static bool IsAdministratorActivation(AppActivationArguments appActivationArguments) => string.Equals(GetActivationToken(appActivationArguments), AdministratorActivationToken, StringComparison.OrdinalIgnoreCase);

    private static string? GetActivationToken(AppActivationArguments appActivationArguments) => appActivationArguments.Kind switch
    {
        ExtendedActivationKind.Launch when appActivationArguments.Data is ILaunchActivatedEventArgs launchActivatedEventArgs => ParseActivationToken(launchActivatedEventArgs.Arguments),
        ExtendedActivationKind.Protocol when appActivationArguments.Data is IProtocolActivatedEventArgs protocolActivatedEventArgs => ParseActivationToken(protocolActivatedEventArgs.Uri),
        _ => null
    };

    private static string? ParseActivationToken(string? commandLineArguments)
    {
        if (string.IsNullOrWhiteSpace(commandLineArguments)) return null;
        return Uri.TryCreate(commandLineArguments.Trim(), UriKind.Absolute, out var protocolActivationUri) ? ParseActivationToken(protocolActivationUri) : null;
    }

    private static string? ParseActivationToken(Uri? protocolActivationUri)
    {
        if (protocolActivationUri is null || !string.Equals(protocolActivationUri.Scheme, ActivationProtocolName, StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.IsNullOrWhiteSpace(protocolActivationUri.Host)) return protocolActivationUri.Host;

        var normalizedAbsolutePath = protocolActivationUri.AbsolutePath.Trim('/');
        return string.IsNullOrWhiteSpace(normalizedAbsolutePath) ? null : normalizedAbsolutePath;
    }

    private static void RegisterTask()
    {
        using var taskService = new TaskService();
        var taskDefinition = taskService.NewTask();
        taskDefinition.RegistrationInfo.Description = "Launch CrispShot on demand with administrator privileges.";
        taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;
        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
        taskDefinition.Settings.AllowDemandStart = true;
        taskDefinition.Settings.DisallowStartIfOnBatteries = false;
        taskDefinition.Settings.StopIfGoingOnBatteries = false;
        taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        taskDefinition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
        taskDefinition.Actions.Add(new ExecAction(GetCommandProcessorPath(), CreateCommandProcessorArguments(), Path.GetDirectoryName(GetCommandProcessorPath())));
        taskService.RootFolder.RegisterTaskDefinition(AdministratorScheduledTaskName, taskDefinition, TaskCreation.CreateOrUpdate, null, null, TaskLogonType.InteractiveToken);
    }

    private static void DeleteTask()
    {
        using var taskService = new TaskService();
        if (taskService.GetTask(AdministratorScheduledTaskName) is not null) taskService.RootFolder.DeleteTask(AdministratorScheduledTaskName, exceptionOnNotExists: false);
    }

    private static string CreateCommandProcessorArguments() => $"/c start \"\" \"{ActivationProtocolName}://{AdministratorActivationToken}\"";

    private static string GetCommandProcessorPath() => Path.Combine(Environment.SystemDirectory, "cmd.exe");
}