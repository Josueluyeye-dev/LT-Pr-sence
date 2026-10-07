using System.Threading;
using System.Windows;
using MelodyPresence.Data;
using MelodyPresence.Models;
using MelodyPresence.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\LT.Services.Presence.Mutex";
    private const string ShowEventName = @"Local\LT.Services.Presence.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private CancellationTokenSource? _listenCts;
    private MainWindow? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!createdNew)
        {
            try { _showSignal.Set(); } catch { /* ignore */ }
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        PresenceDbContext.Initialiser();
        SynchroniserDemarrageWindows();

        var autostart = e.Args.Any(a =>
            string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        if (!DemanderConnexion())
        {
            Shutdown();
            return;
        }

        OuvrirApplicationPrincipale(autostart);

        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                if (_showSignal.WaitOne(400))
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!AuthService.EstConnecte)
                        {
                            if (!DemanderConnexion())
                                return;
                            OuvrirApplicationPrincipale(autostart: false);
                            return;
                        }

                        _main?.RestaurerDepuisTray();
                    });
                }
            }
        }, token);
    }

    private bool DemanderConnexion()
    {
        var login = new LoginWindow();
        return login.ShowDialog() == true && AuthService.EstConnecte;
    }

    private void OuvrirApplicationPrincipale(bool autostart)
    {
        if (_main != null)
        {
            _main.RestaurerDepuisTray();
            return;
        }

        _main = new MainWindow();
        MainWindow = _main;
        WindowsNotificationService.BrancherActivation(() =>
            Dispatcher.Invoke(() =>
            {
                if (!AuthService.EstConnecte)
                {
                    if (DemanderConnexion())
                        OuvrirApplicationPrincipale(false);
                    return;
                }

                _main?.RestaurerDepuisTray();
            }));
        _main.Show();

        if (autostart)
            _main.MasquerDansTray(premierMasquage: true);
        else
            _main.RestaurerDepuisTray();
    }

    public void DeconnexionEtRelancerLogin()
    {
        AuthService.Deconnecter();
        if (_main != null)
        {
            _main.AllowClose = true;
            _main.Close();
            _main = null;
            MainWindow = null;
        }

        if (!DemanderConnexion())
        {
            Shutdown();
            return;
        }

        OuvrirApplicationPrincipale(autostart: false);
    }

    private static void SynchroniserDemarrageWindows()
    {
        try
        {
            using var db = new PresenceDbContext();
            var p = db.Parametres.AsNoTracking()
                .FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
            DemarrageWindowsService.Appliquer(p?.DemarrerAvecWindows ?? true);
        }
        catch
        {
            // non bloquant
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _listenCts?.Cancel(); } catch { /* ignore */ }
        try { _listenCts?.Dispose(); } catch { /* ignore */ }
        try { _showSignal?.Dispose(); } catch { /* ignore */ }
        try
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
        catch { /* ignore */ }

        base.OnExit(e);
    }
}
