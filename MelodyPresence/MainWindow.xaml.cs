using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MelodyPresence.ViewModels;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using DrawingIcon = System.Drawing.Icon;

namespace MelodyPresence;

public partial class MainWindow : Window
{
    private Forms.NotifyIcon? _tray;
    private bool _fermetureDefinitive;
    private bool _ballonMasquageDejaAffiche;
    private bool _pretPourTray;

    /// <summary>Autorise la fermeture réelle (déconnexion / quitter).</summary>
    public bool AllowClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        InitialiserTray();
        StateChanged += (_, _) =>
        {
            // Évite de masquer au tout premier affichage (certains PC envoient Minimized un instant).
            if (!_pretPourTray) return;
            if (WindowState == WindowState.Minimized)
                MasquerDansTray(premierMasquage: false);
        };
    }

    private void InitialiserTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Ouvrir LT Services", null, (_, _) => RestaurerDepuisTray());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => QuitterApplication());

        _tray = new Forms.NotifyIcon
        {
            Text = "LT Services Présence",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = ChargerIcone()
        };
        _tray.DoubleClick += (_, _) => RestaurerDepuisTray();
    }

    private static DrawingIcon ChargerIcone()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/lt_services.ico");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo?.Stream != null)
                return new DrawingIcon(streamInfo.Stream, 32, 32);
        }
        catch
        {
            // fallback
        }

        return SystemIcons.Application;
    }

    public void MasquerDansTray(bool premierMasquage)
    {
        ShowInTaskbar = false;
        Hide();
        WindowState = WindowState.Normal;

        if (_tray == null) return;

        if (premierMasquage || !_ballonMasquageDejaAffiche)
        {
            _ballonMasquageDejaAffiche = true;
            _tray.BalloonTipTitle = "LT Services Présence";
            _tray.BalloonTipText = premierMasquage
                ? "L’application tourne en arrière-plan au démarrage de Windows."
                : "L’application reste ouverte dans la zone de notification.";
            _tray.ShowBalloonTip(2500);
        }
    }

    public void RestaurerDepuisTray()
    {
        Show();
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void QuitterApplication()
    {
        _fermetureDefinitive = true;
        AllowClose = true;
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose && !_fermetureDefinitive)
        {
            e.Cancel = true;
            MasquerDansTray(premierMasquage: false);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        base.OnClosed(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _pretPourTray = true;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ContentHost.BeginAnimation(OpacityProperty, fade);
    }

    private void ExporterPresence_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || DataContext is not MainViewModel vm)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = btn,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        menu.Items.Add(new MenuItem
        {
            Header = "Excel (.xlsx)",
            Command = vm.ExportExcelJourCommand
        });
        menu.Items.Add(new MenuItem
        {
            Header = "PDF",
            Command = vm.ExportPdfJourCommand
        });
        menu.Items.Add(new MenuItem
        {
            Header = "CSV",
            Command = vm.ExportCsvJourCommand
        });

        btn.ContextMenu = menu;
        menu.IsOpen = true;
    }
}
