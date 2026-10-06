using System.IO;
using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MelodyPresence.Services;

public enum ToastKind
{
    Info,
    SyncSuccess,
    SyncError,
    Error
}

/// <summary>
/// Toasts Windows charte LT — hero dégradé rouge→noir, logo circulaire, copy soignée.
/// </summary>
public static class WindowsNotificationService
{
    private const string Attribution = "LT Services · vous êtes dans de bonnes mains";
    private static bool _activationBranched;

    public static void BrancherActivation(Action? ouvrirApp)
    {
        if (_activationBranched)
            return;
        _activationBranched = true;
        try
        {
            ToastNotificationManagerCompat.OnActivated += args =>
            {
                var action = args.Argument;
                if (string.IsNullOrEmpty(action) || action.Contains("open") || action.Contains("action=open"))
                    ouvrirApp?.Invoke();
            };
        }
        catch
        {
            // OS sans support toast — ignorer
        }
    }

    public static bool SontActives()
    {
        try
        {
            using var db = new PresenceDbContext();
            var p = db.Parametres.AsNoTracking()
                .FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
            return p?.NotificationsWindowsActives ?? true;
        }
        catch
        {
            return true;
        }
    }

    public static void NotifierInfo(string message, string? titre = null)
        => Afficher(ToastKind.Info, titre ?? "LT Présence", message);

    public static void NotifierErreur(string message, string? titre = null)
        => Afficher(ToastKind.Error, titre ?? "Alerte LT Présence", message);

    public static void NotifierSyncSucces(int nbPointages)
    {
        var titre = nbPointages <= 0
            ? "Réseau calme"
            : nbPointages == 1
                ? "1 pointage capté"
                : $"{nbPointages} pointages captés";
        var corps = nbPointages <= 0
            ? "Synchronisation terminée — aucun nouveau mouvement."
            : "La journée se met à jour. Présence synchronisée avec le terminal.";
        Afficher(ToastKind.SyncSuccess, titre, corps, headerId: "sync", headerTitle: "Synchronisation");
    }

    public static void NotifierSyncErreur(string message)
        => Afficher(
            ToastKind.SyncError,
            "Signal interrompu",
            string.IsNullOrWhiteSpace(message)
                ? "Impossible de joindre le terminal ZK."
                : message.Trim(),
            headerId: "sync",
            headerTitle: "Synchronisation");

    public static void NotifierApercuDesign()
        => Afficher(
            ToastKind.Info,
            "Charte LT activée",
            "Notifications cinéma : dégradé rouge → noir, logo LT, signature officielle.",
            headerId: "design",
            headerTitle: "LT Services");

    private static void Afficher(
        ToastKind kind,
        string titre,
        string message,
        string? headerId = null,
        string? headerTitle = null)
    {
        if (!SontActives())
            return;
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(titre))
            return;

        try
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "open")
                .AddText(titre.Trim(), AdaptiveTextStyle.Header)
                .AddText(message.Trim(), AdaptiveTextStyle.Body)
                .AddAttributionText(Attribution);

            if (!string.IsNullOrWhiteSpace(headerId) && !string.IsNullOrWhiteSpace(headerTitle))
                builder.AddHeader(headerId, headerTitle, headerId);

            var logo = CheminAsset("toast_logo_circle.png")
                       ?? CheminAsset("lt_services_icon.png");
            if (logo != null)
            {
                builder.AddAppLogoOverride(
                    new Uri(logo),
                    ToastGenericAppLogoCrop.Circle);
            }

            var hero = kind switch
            {
                ToastKind.SyncSuccess => CheminAsset("toast_hero_sync.png"),
                ToastKind.SyncError or ToastKind.Error => CheminAsset("toast_hero_error.png"),
                _ => CheminAsset("toast_hero_info.png")
            };
            if (hero != null)
                builder.AddHeroImage(new Uri(hero));

            builder.AddButton(new ToastButton()
                .SetContent("Ouvrir LT Présence")
                .AddArgument("action", "open")
                .SetBackgroundActivation());

            builder.AddAudio(new Uri(AudioPour(kind)));

            builder.Show(toast =>
            {
                toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(kind is ToastKind.Error or ToastKind.SyncError ? 10 : 4);
                toast.Tag = kind.ToString();
                toast.Group = "LT.Presence";
            });
        }
        catch
        {
            // Toast OS non critique — le bandeau in-app reste la source de vérité.
        }
    }

    private static string AudioPour(ToastKind kind) => kind switch
    {
        ToastKind.SyncSuccess => "ms-winsoundevent:Notification.IM",
        ToastKind.SyncError or ToastKind.Error => "ms-winsoundevent:Notification.Reminder",
        _ => "ms-winsoundevent:Notification.Default"
    };

    private static string? CheminAsset(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", fileName),
            Path.Combine(baseDir, fileName),
            Path.Combine(baseDir, "..", "Assets", fileName),
        };
        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full))
                    return full;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }
}
