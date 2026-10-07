namespace MelodyPresence.Services;

/// <summary>
/// Canal de mise à jour GitHub (dépôt public requis pour les clients).
/// </summary>
public static class ApplicationUpdateDefaults
{
    public const string GitHubRepo = "Josueluyeye-dev/LT-Pr-sence";

    public const string ManifestUrlParDefaut =
        "https://raw.githubusercontent.com/Josueluyeye-dev/LT-Pr-sence/main/installer/updates/version.json";

    public static string ReleasesLatestApiUrl =>
        $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
}
