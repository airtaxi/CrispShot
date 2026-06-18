using CrispShot.Enums;

namespace CrispShot.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = true;

    public ShadowIntensity ShadowIntensity { get; set; } = ShadowIntensity.Medium;

    public bool ProcessWhenNoAlpha { get; set; } = true;

    public AppLanguagePreference LanguagePreference { get; set; } = AppLanguagePreference.System;
}

