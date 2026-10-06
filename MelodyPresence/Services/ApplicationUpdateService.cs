using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MelodyPresence.Data;
using MelodyPresence.Models;

namespace MelodyPresence.Services;

public enum UpdateCheckResultKind
{
    UpToDate,
    UpdateAvailable,
    Error
}

public sealed class UpdateCheckResult
{
    public UpdateCheckResultKind Kind { get; init; }
    public string Message { get; init; } = "";
    public UpdateManifest? Manifest { get; init; }
    public Version? VersionInstallee { get; init; }
    public Version? VersionDisponible { get; init; }
}

public sealed class UpdateDownloadResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? CheminInstallateur { get; set; }
}

/// <summary>
/// Vérifie version.json / GitHub Releases, télécharge et lance l'installateur.
/// </summary>
public static class ApplicationUpdateService
{
    private static readonly HttpClient Http = CreerClient(TimeSpan.FromSeconds(45));
    private static readonly HttpClient HttpDl = CreerClient(TimeSpan.FromMinutes(20));
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static HttpClient CreerClient(TimeSpan timeout)
    {
        var c = new HttpClient { Timeout = timeout };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LT-Presence-Updater/1.0");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return c;
    }

    public static string DossierTelechargements =>
        Path.Combine(
            Path.GetDirectoryName(PresenceDbContext.CheminBaseDeDonnees) ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MelodyPresence"),
            "Updates");

    public static Version ObtenirVersionInstallee()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var partie = info.Split('+')[0].Trim();
            if (Version.TryParse(partie, out var vInfo))
                return vInfo;
        }

        var v = asm.GetName().Version;
        return v != null
            ? new Version(v.Major, v.Minor, Math.Max(0, v.Build))
            : new Version(1, 0, 0);
    }

    public static string FormaterVersion(Version version) =>
        version.Revision > 0 ? version.ToString(4) : version.ToString(3);

    public static async Task<UpdateCheckResult> VerifierAsync(CancellationToken cancellationToken = default)
    {
        var installee = ObtenirVersionInstallee();

        try
        {
            var depuisManifeste = await VerifierDepuisManifesteAsync(
                ApplicationUpdateDefaults.ManifestUrlParDefaut, installee, cancellationToken)
                .ConfigureAwait(false);
            if (depuisManifeste.Kind != UpdateCheckResultKind.Error ||
                !depuisManifeste.Message.Contains("404", StringComparison.Ordinal))
                return depuisManifeste;
        }
        catch
        {
            // fallback Releases
        }

        try
        {
            return await VerifierDepuisGitHubReleasesAsync(installee, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // message utilisateur
        }

        return new UpdateCheckResult
        {
            Kind = UpdateCheckResultKind.Error,
            Message =
                "Impossible de joindre le serveur de mises à jour. " +
                "Vérifiez votre connexion Internet, puis réessayez.",
            VersionInstallee = installee
        };
    }

    private static async Task<UpdateCheckResult> VerifierDepuisManifesteAsync(
        string url, Version installee, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "URL du manifeste invalide.",
                VersionInstallee = installee
            };
        }

        using var response = await Http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = $"Serveur de mises à jour : {(int)response.StatusCode}.",
                VersionInstallee = installee
            };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
        return EvaluerManifeste(manifest, installee);
    }

    private static async Task<UpdateCheckResult> VerifierDepuisGitHubReleasesAsync(
        Version installee, CancellationToken cancellationToken)
    {
        var json = await Http.GetStringAsync(ApplicationUpdateDefaults.ReleasesLatestApiUrl, cancellationToken)
            .ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var versionText = tag.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out _))
            throw new InvalidOperationException($"Tag invalide : {tag}");

        string? downloadUrl = null;
        string? fileName = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                fileName = name;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException("Aucun installateur .exe dans la release.");

        var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
        return EvaluerManifeste(new UpdateManifest
        {
            Version = versionText,
            DownloadUrl = downloadUrl,
            FileName = fileName,
            ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? $"Release {tag}" : notes
        }, installee);
    }

    public static UpdateCheckResult EvaluerManifeste(UpdateManifest? manifest, Version installee)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "Le fichier de version est vide ou illisible.",
                VersionInstallee = installee
            };
        }

        if (!Version.TryParse(manifest.Version.Trim(), out var disponible))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = $"Numéro de version invalide : « {manifest.Version} ».",
                VersionInstallee = installee
            };
        }

        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl) && disponible > installee)
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "Mise à jour signalée mais sans lien de téléchargement.",
                VersionInstallee = installee,
                VersionDisponible = disponible,
                Manifest = manifest
            };
        }

        if (disponible <= installee)
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.UpToDate,
                Message = $"Vous utilisez la dernière version ({FormaterVersion(installee)}).",
                VersionInstallee = installee,
                VersionDisponible = disponible,
                Manifest = manifest
            };
        }

        return new UpdateCheckResult
        {
            Kind = UpdateCheckResultKind.UpdateAvailable,
            Message = $"La version {FormaterVersion(disponible)} est disponible (installée : {FormaterVersion(installee)}).",
            VersionInstallee = installee,
            VersionDisponible = disponible,
            Manifest = manifest
        };
    }

    public static async Task<UpdateDownloadResult> TelechargerAsync(
        UpdateManifest manifest,
        IProgress<double>? progression = null,
        CancellationToken cancellationToken = default)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            return new UpdateDownloadResult { Success = false, Message = "URL de téléchargement manquante." };

        if (!Uri.TryCreate(manifest.DownloadUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return new UpdateDownloadResult { Success = false, Message = "URL de téléchargement invalide." };

        var nom = !string.IsNullOrWhiteSpace(manifest.FileName)
            ? Path.GetFileName(manifest.FileName)
            : Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(nom) || !nom.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            nom = $"LT_Presence_Setup_{manifest.Version}.exe";

        Directory.CreateDirectory(DossierTelechargements);
        var cheminFinal = Path.Combine(DossierTelechargements, nom);
        var cheminPartiel = cheminFinal + ".part";
        try { if (File.Exists(cheminPartiel)) File.Delete(cheminPartiel); } catch { /* ignore */ }

        try
        {
            using var response = await HttpDl
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            string empreinte;
            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var fichier = new FileStream(cheminPartiel, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long lu = 0;
                int n;
                while ((n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fichier.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
                    hasher.AppendData(buffer.AsSpan(0, n));
                    lu += n;
                    if (total is > 0)
                        progression?.Report(Math.Min(99, lu * 99.0 / total.Value));
                }

                await fichier.FlushAsync(cancellationToken).ConfigureAwait(false);
                empreinte = Convert.ToHexString(hasher.GetHashAndReset());
            }

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var attendu = manifest.Sha256.Trim().Replace(" ", "", StringComparison.Ordinal);
                if (!string.Equals(empreinte, attendu, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(cheminPartiel); } catch { /* ignore */ }
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        Message = "Le fichier téléchargé est altéré. Réessayez."
                    };
                }
            }

            if (File.Exists(cheminFinal))
                File.Delete(cheminFinal);
            File.Move(cheminPartiel, cheminFinal);
            progression?.Report(100);
            return new UpdateDownloadResult
            {
                Success = true,
                Message = "Téléchargement terminé.",
                CheminInstallateur = cheminFinal
            };
        }
        catch (OperationCanceledException)
        {
            return new UpdateDownloadResult { Success = false, Message = "Téléchargement annulé." };
        }
        catch
        {
            return new UpdateDownloadResult
            {
                Success = false,
                Message = "Échec du téléchargement. Vérifiez la connexion, puis réessayez."
            };
        }
    }

    public static bool LancerInstallateur(string chemin, out string message)
    {
        message = "";
        if (string.IsNullOrWhiteSpace(chemin) || !File.Exists(chemin))
        {
            message = "Fichier d'installation introuvable.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = chemin,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(chemin) ?? DossierTelechargements
            });
            return true;
        }
        catch (Exception ex)
        {
            message = $"Impossible de lancer l'installateur : {ex.Message}";
            return false;
        }
    }
}
