using CrispShot.Enums;
using CrispShot.Models;
using Windows.Storage;

namespace CrispShot.Services;

public sealed class SettingsService
{
    private const string StartWithWindowsKey = nameof(AppSettings.StartWithWindows);
    private const string ShadowIntensityKey = nameof(AppSettings.ShadowIntensity);
    private const string ProcessWhenNoAlphaKey = nameof(AppSettings.ProcessWhenNoAlpha);
    private const string LanguagePreferenceKey = nameof(AppSettings.LanguagePreference);
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public SettingsService()
    {
        Current = Load();
    }

    public event EventHandler? SettingsChanged;

    public AppSettings Current { get; }

    public void SetStartWithWindows(bool value) => UpdateSetting(StartWithWindowsKey, value, (settings, settingValue) => settings.StartWithWindows = settingValue);

    public void SetShadowIntensity(ShadowIntensity value) => UpdateSetting(ShadowIntensityKey, (int)value, (settings, settingValue) => settings.ShadowIntensity = (ShadowIntensity)settingValue);

    public void SetProcessWhenNoAlpha(bool value) => UpdateSetting(ProcessWhenNoAlphaKey, value, (settings, settingValue) => settings.ProcessWhenNoAlpha = settingValue);

    public void SetLanguagePreference(AppLanguagePreference value) => UpdateSetting(LanguagePreferenceKey, (int)value, (settings, settingValue) => settings.LanguagePreference = (AppLanguagePreference)settingValue);

    private AppSettings Load()
    {
        var settings = new AppSettings
        {
            StartWithWindows = ReadBoolean(StartWithWindowsKey, true),
            ShadowIntensity = (ShadowIntensity)ReadInt32(ShadowIntensityKey, (int)ShadowIntensity.Medium),
            ProcessWhenNoAlpha = ReadBoolean(ProcessWhenNoAlphaKey, true),
            LanguagePreference = (AppLanguagePreference)ReadInt32(LanguagePreferenceKey, (int)AppLanguagePreference.System),
        };

        return settings;
    }

    private bool ReadBoolean(string key, bool defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is bool booleanValue) return booleanValue;
        _localSettings.Values[key] = defaultValue;
        return defaultValue;
    }

    private int ReadInt32(string key, int defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is int integerValue) return integerValue;
        _localSettings.Values[key] = defaultValue;
        return defaultValue;
    }

    private void UpdateSetting<TValue>(string key, TValue value, Action<AppSettings, TValue> updateSetting)
    {
        updateSetting(Current, value);
        _localSettings.Values[key] = value;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
