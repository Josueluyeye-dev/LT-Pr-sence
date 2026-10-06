using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public static class ZktecoSynchronisationService
{
    private static DispatcherTimer? _timer;
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static int _syncEnCours;

    public static event Action<DateTime, int>? SynchroReussie;
    public static event Action? SynchroEnCours;
    public static event Action<string>? SynchroErreur;

    public static void Reconfigurer()
    {
        _timer?.Stop();
        _timer = null;

        if (Application.Current?.Dispatcher == null)
            return;

        using var db = new PresenceDbContext();
        var p = db.Parametres.AsNoTracking().FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
        if (p == null || !p.ZkSyncActif || string.IsNullOrWhiteSpace(p.ZkTerminalIp))
            return;

        var sec = Math.Clamp(p.ZkIntervalleSecondes <= 0 ? 60 : p.ZkIntervalleSecondes, 5, 3600);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(sec) };
        _timer.Tick += async (_, _) => await TrySynchroniserSansChevauchementAsync();
        _timer.Start();
        _ = TrySynchroniserSansChevauchementAsync();
    }

    public static bool TrySynchroniser(out string? messageErreur, out int nbNouveaux)
    {
        SyncLock.Wait();
        try
        {
            return Execute(out messageErreur, out nbNouveaux);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    public static async Task<(bool Ok, string? Err, int Nb)> TrySynchroniserAsync(
        CancellationToken cancellationToken = default)
    {
        await SyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SynchroEnCours?.Invoke();
            return await Task.Run(() =>
            {
                var ok = Execute(out var err, out var nb);
                return (ok, err, nb);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private static bool Execute(out string? messageErreur, out int nbNouveaux)
    {
        messageErreur = null;
        nbNouveaux = 0;
        try
        {
            using var db = new PresenceDbContext();
            var p = db.Parametres.FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
            if (p == null || string.IsNullOrWhiteSpace(p.ZkTerminalIp))
            {
                messageErreur = "Adresse IP du terminal non configurée.";
                return false;
            }

            var ip = p.ZkTerminalIp.Trim();
            var port = p.ZkTerminalPort > 0 ? p.ZkTerminalPort : 4370;
            var machine = p.ZkMachineNumber > 0 ? p.ZkMachineNumber : 1;

            IReadOnlyList<(string CodePin, DateTime Horodatage)> logs;
            try
            {
                logs = ZktecoPointageReader.Lire(ip, port, machine, p.ZkCommPassword);
            }
            catch (Exception ex)
            {
                messageErreur = ex.Message;
                return false;
            }

            nbNouveaux = new PointageService().FusionnerDepuisTerminal(logs);
            p.ZkDerniereSyncUtc = DateTime.UtcNow;
            db.SaveChanges();
            SynchroReussie?.Invoke(p.ZkDerniereSyncUtc.Value, nbNouveaux);
            return true;
        }
        catch (Exception ex)
        {
            messageErreur = ex.Message;
            return false;
        }
    }

    private static async Task TrySynchroniserSansChevauchementAsync()
    {
        if (Interlocked.Exchange(ref _syncEnCours, 1) == 1)
            return;
        try
        {
            var (ok, msg, _) = await TrySynchroniserAsync().ConfigureAwait(true);
            if (!ok)
                SynchroErreur?.Invoke(msg ?? "Erreur synchronisation ZKTeco.");
        }
        finally
        {
            Interlocked.Exchange(ref _syncEnCours, 0);
        }
    }
}
