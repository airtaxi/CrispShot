using Windows.ApplicationModel;

namespace CrispShot.Services;

public static class AssetPathResolver
{
    public static string IconFilePath
    {
        get
        {
            foreach (var candidatePath in GetIconCandidatePaths())
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return Path.Combine(AppContext.BaseDirectory, "Assets", "Icon.ico");
        }
    }

    private static IEnumerable<string> GetIconCandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Icon.ico");

        string? installedLocationPath = null;
        try { installedLocationPath = Package.Current.InstalledLocation.Path; }
        catch (Exception) { }

        if (!string.IsNullOrWhiteSpace(installedLocationPath)) yield return Path.Combine(installedLocationPath, "Assets", "Icon.ico");
    }
}
