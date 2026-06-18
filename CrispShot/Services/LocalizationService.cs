using CrispShot.Enums;
using CrispShot.Models;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using System.Globalization;

namespace CrispShot.Services;

public sealed class LocalizationService(SettingsService settingsService)
{
    private static readonly List<string> s_installedLanguages = ["en-US", "ko-KR", "ja-JP", "zh-Hans", "zh-Hant"];

    private readonly SettingsService _settingsService = settingsService;
    private ResourceLoader _resourceLoader = new();

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<AppLanguagePreference> AvailablePreferences { get; } =
        [AppLanguagePreference.System, AppLanguagePreference.Korean, AppLanguagePreference.English, AppLanguagePreference.Japanese, AppLanguagePreference.ChineseSimplified, AppLanguagePreference.ChineseTraditional];

    public AppLanguagePreference CurrentLanguagePreference { get; private set; } = AppLanguagePreference.System;

    public void Initialize()
    {
        var languagePreference = _settingsService.Current.LanguagePreference;
        ApplyLanguagePreferenceOverride(languagePreference);
        CurrentLanguagePreference = languagePreference;
    }

    public void ApplyLanguagePreference(AppLanguagePreference appLanguagePreference)
    {
        if (CurrentLanguagePreference == appLanguagePreference) return;

        ApplyLanguagePreferenceOverride(appLanguagePreference);
        _resourceLoader = new ResourceLoader();
        CurrentLanguagePreference = appLanguagePreference;
        _settingsService.SetLanguagePreference(appLanguagePreference);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string resourceName)
    {
        var normalizedResourceName = resourceName.Replace('.', '/');
        var localizedString = _resourceLoader.GetString(normalizedResourceName);
        return string.IsNullOrWhiteSpace(localizedString) ? resourceName : localizedString;
    }

    public string GetLanguageDisplayName(AppLanguagePreference appLanguagePreference) => appLanguagePreference switch
    {
        AppLanguagePreference.System => GetString("Tray.Language.System"),
        AppLanguagePreference.Korean => "한국어",
        AppLanguagePreference.English => "English",
        AppLanguagePreference.Japanese => "日本語",
        AppLanguagePreference.ChineseSimplified => "简体中文",
        AppLanguagePreference.ChineseTraditional => "繁體中文",
        _ => appLanguagePreference.ToString(),
    };

    private static void ApplyLanguagePreferenceOverride(AppLanguagePreference appLanguagePreference)
    {
        var languageTag = GetLanguageTag(appLanguagePreference);
        ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        ApplyCurrentThreadCultures(languageTag);
    }

    private static void ApplyCurrentThreadCultures(string languageTag)
    {
        var resolvedLanguageTag = string.IsNullOrWhiteSpace(languageTag) ? ApplicationLanguages.Languages.Count > 0 ? ApplicationLanguages.Languages[0] : string.Empty : languageTag;
        if (string.IsNullOrWhiteSpace(resolvedLanguageTag)) return;

        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(resolvedLanguageTag);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
        catch (CultureNotFoundException) { }
    }

    private static string GetLanguageTag(AppLanguagePreference appLanguagePreference) => appLanguagePreference switch
    {
        AppLanguagePreference.System => GetDefaultLanguageTag(),
        AppLanguagePreference.Korean => "ko-KR",
        AppLanguagePreference.English => "en-US",
        AppLanguagePreference.Japanese => "ja-JP",
        AppLanguagePreference.ChineseSimplified => "zh-Hans",
        AppLanguagePreference.ChineseTraditional => "zh-Hant",
        _ => string.Empty,
    };

    private static string GetDefaultLanguageTag()
    {
        var installedUserInterfaceCultureName = CultureInfo.InstalledUICulture.Name;
        return s_installedLanguages.Contains(installedUserInterfaceCultureName) ? installedUserInterfaceCultureName : s_installedLanguages.First();
    }
}
