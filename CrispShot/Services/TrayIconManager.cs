using CrispShot.Enums;
using CrispShot.Models;
using DevWinUI;
using Microsoft.UI.Xaml.Controls;

namespace CrispShot.Services;

public sealed class TrayIconManager(SettingsService settingsService, StartupTaskService startupTaskService, AdministratorRunService administratorRunService, LocalizationService localizationService) : IDisposable
{
    private const uint TrayIconIdentifier = 1;
    private SystemTrayIcon? _trayIcon;
    private bool _disposed;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon is not null) return;

        _trayIcon = new SystemTrayIcon(TrayIconIdentifier, AssetPathResolver.IconFilePath, "CrispShot");
        _trayIcon.LeftClick += OnTrayIconClicked;
        _trayIcon.RightClick += OnTrayIconClicked;
        _trayIcon.IsVisible = true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_trayIcon is not null)
        {
            _trayIcon.LeftClick -= OnTrayIconClicked;
            _trayIcon.RightClick -= OnTrayIconClicked;
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _disposed = true;
    }

    private void OnTrayIconClicked(SystemTrayIcon _, SystemTrayIconEventArgs args) => args.Flyout = CreateMenuFlyout();

    private MenuFlyout CreateMenuFlyout()
    {
        var flyout = new MenuFlyout();

        flyout.Items.Add(CreateShadowIntensityItem());
        flyout.Items.Add(CreateToggleItem(localizationService.GetString("Tray.ProcessWhenNoAlpha"), settingsService.Current.ProcessWhenNoAlpha, settingsService.SetProcessWhenNoAlpha));
        flyout.Items.Add(CreateStartupTaskToggleItem());
        flyout.Items.Add(CreateAdministratorRunItem());
        flyout.Items.Add(CreateLanguageItem());
        flyout.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = localizationService.GetString("Tray.Exit") };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        flyout.Items.Add(exitItem);

        return flyout;
    }

    private static ToggleMenuFlyoutItem CreateToggleItem(string text, bool isChecked, Action<bool> applyValue)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = text,
            IsChecked = isChecked
        };
        item.Click += (_, _) => applyValue(item.IsChecked);
        return item;
    }

    private MenuFlyoutSubItem CreateShadowIntensityItem()
    {
        var item = new MenuFlyoutSubItem { Text = localizationService.GetString("Tray.ShadowEffect") };
        item.Items.Add(CreateShadowIntensityToggleItem("Tray.Shadow.Off", ShadowIntensity.Off));
        item.Items.Add(CreateShadowIntensityToggleItem("Tray.Shadow.Low", ShadowIntensity.Low));
        item.Items.Add(CreateShadowIntensityToggleItem("Tray.Shadow.Medium", ShadowIntensity.Medium));
        item.Items.Add(CreateShadowIntensityToggleItem("Tray.Shadow.High", ShadowIntensity.High));
        return item;
    }

    private ToggleMenuFlyoutItem CreateShadowIntensityToggleItem(string resourceName, ShadowIntensity shadowIntensity)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = localizationService.GetString(resourceName),
            IsChecked = settingsService.Current.ShadowIntensity == shadowIntensity,
        };
        item.Click += (_, _) => settingsService.SetShadowIntensity(shadowIntensity);
        return item;
    }

    private ToggleMenuFlyoutItem CreateStartupTaskToggleItem()
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = localizationService.GetString("Tray.StartWithWindows"),
            IsChecked = settingsService.Current.StartWithWindows
        };

        item.Click += async (_, _) =>
        {
            var wasApplied = await startupTaskService.SetStartWithWindowsEnabledAsync(item.IsChecked);
            if (!wasApplied) item.IsChecked = settingsService.Current.StartWithWindows;
        };

        return item;
    }

    private ToggleMenuFlyoutItem CreateAdministratorRunItem()
    {
        var registrationState = administratorRunService.GetRegistrationState();
        var isRegistered = registrationState == AdministratorRunRegistrationState.Registered;
        var isCurrentProcessElevated = administratorRunService.IsCurrentProcessElevated;
        var text = localizationService.GetString("Tray.AlwaysRunAsAdministrator");
        if (!isCurrentProcessElevated) text += localizationService.GetString("Tray.RequiresAdministratorPostfix");

        var item = new ToggleMenuFlyoutItem
        {
            Text = text,
            IsChecked = isRegistered,
            IsEnabled = isCurrentProcessElevated,
        };

        item.Click += async (_, _) =>
        {
            var wasApplied = await administratorRunService.SetEnabledAsync(!isRegistered);
            if (!wasApplied) System.Diagnostics.Debug.WriteLine("CrispShot administrator task menu action failed.");
        };

        return item;
    }

    private MenuFlyoutSubItem CreateLanguageItem()
    {
        var item = new MenuFlyoutSubItem { Text = localizationService.GetString("Tray.LanguageGroup") };
        foreach (var languagePreference in localizationService.AvailablePreferences) item.Items.Add(CreateLanguageToggleItem(languagePreference));

        return item;
    }

    private ToggleMenuFlyoutItem CreateLanguageToggleItem(AppLanguagePreference languagePreference)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = localizationService.GetLanguageDisplayName(languagePreference),
            IsChecked = localizationService.CurrentLanguagePreference == languagePreference,
        };
        item.Click += (_, _) => localizationService.ApplyLanguagePreference(languagePreference);
        return item;
    }
}
