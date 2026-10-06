namespace MelodyPresence.Services;

/// <summary>
/// Canal de mise à jour GitHub (dépôt public requis pour les clients).
/// </summary>
public static class ApplicationUpdateDefaults
{
    public const string GitHubRepo = "Mavuisra/MelodyPresence";

    public const string ManifestUrlParDefaut =
        "https://raw.githubusercontent.com/Mavuisra/MelodyPresence/master/installer/updates/version.json";

    public static string ReleasesLatestApiUrl =>
        $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
}
