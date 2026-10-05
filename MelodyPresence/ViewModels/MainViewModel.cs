using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MelodyPresence.Data;
using MelodyPresence.Helpers;
using MelodyPresence.Models;
using MelodyPresence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace MelodyPresence.ViewModels;

public class MainViewModel : ObservableObject
{
    private string _onglet = "Accueil";
    private string _statutBarre = "Prêt";
    private string _messageErreur = "";
    private DateTime _datePresence = DateTime.Today;
    private int _moisRapport = DateTime.Today.Month;
    private int _anneeRapport = DateTime.Today.Year;
    private Employe? _employeSelectionne;
    private Employe? _employeEdition;
    private JourPresenceLigne? _ligneJourSelectionnee;
    private string _filtreEmploye = "";

    // Paramètres
    private string _nomEntreprise = "";
    private string _zkIp = "";
    private string _zkPort = "4370";
    private string _zkMachine = "1";
    private string _zkCommPwd = "000000";
    private bool _zkSyncActif;
    private string _zkIntervalle = "60";
    private string _zkDerniereSync = "Jamais";

    public MainViewModel()
    {
        Employes = new ObservableCollection<Employe>();
        PresenceJour = new ObservableCollection<JourPresenceLigne>();
        ResumeMois = new ObservableCollection<ResumeMoisItem>();
        PointagesRecents = new ObservableCollection<Pointage>();

        NaviguerCommand = new RelayCommand(p => Onglet = p?.ToString() ?? "Accueil");
        RafraichirCommand = new RelayCommand(_ => RafraichirTout());
        NouvelEmployeCommand = new RelayCommand(_ => NouvelEmploye());
        EditerEmployeCommand = new RelayCommand(_ => EditerEmploye(), _ => EmployeSelectionne != null);
        SauverEmployeCommand = new RelayCommand(_ => SauverEmploye(), _ => EmployeEdition != null);
        SupprimerEmployeCommand = new RelayCommand(_ => SupprimerEmploye(), _ => EmployeSelectionne != null);
        PointerEntreeCommand = new RelayCommand(_ => Pointer(PointageType.Entree), _ => LigneJourSelectionnee != null);
        PointerSortieCommand = new RelayCommand(_ => Pointer(PointageType.Sortie), _ => LigneJourSelectionnee != null);
        SynchroniserZkCommand = new RelayCommand(async _ => await SynchroniserZkAsync());
        SauverParametresCommand = new RelayCommand(_ => SauverParametres());
        ChargerRapportCommand = new RelayCommand(_ => ChargerRapportMois());
        ExportExcelJourCommand = new RelayCommand(_ => ExporterExcelJour());
        ExportPdfJourCommand = new RelayCommand(_ => ExporterPdfJour());
        ExportExcelMoisCommand = new RelayCommand(_ => ExporterExcelMois());
        ExportPdfMoisCommand = new RelayCommand(_ => ExporterPdfMois());

        ZktecoSynchronisationService.SynchroReussie += _ =>
            Application.Current?.Dispatcher.Invoke(RafraichirTout);
        ZktecoSynchronisationService.SynchroErreur += err =>
            Application.Current?.Dispatcher.Invoke(() => StatutBarre = err);

        RafraichirTout();
        ChargerParametres();
        ZktecoSynchronisationService.Reconfigurer();
    }

    public ObservableCollection<Employe> Employes { get; }
    public ObservableCollection<JourPresenceLigne> PresenceJour { get; }
    public ObservableCollection<ResumeMoisItem> ResumeMois { get; }
    public ObservableCollection<Pointage> PointagesRecents { get; }

    public string Onglet
    {
        get => _onglet;
        set
        {
            if (SetProperty(ref _onglet, value))
            {
                OnPropertyChanged(nameof(EstAccueil));
                OnPropertyChanged(nameof(EstEmployes));
                OnPropertyChanged(nameof(EstPresence));
                OnPropertyChanged(nameof(EstRapports));
                OnPropertyChanged(nameof(EstParametres));
                if (value == "Présence") ChargerPresenceJour();
                if (value == "Rapports") ChargerRapportMois();
                if (value == "Employés") ChargerEmployes();
                if (value == "Accueil") ChargerAccueil();
                if (value == "Paramètres") ChargerParametres();
            }
        }
    }

    public bool EstAccueil => Onglet == "Accueil";
    public bool EstEmployes => Onglet == "Employés";
    public bool EstPresence => Onglet == "Présence";
    public bool EstRapports => Onglet == "Rapports";
    public bool EstParametres => Onglet == "Paramètres";

    public string StatutBarre
    {
        get => _statutBarre;
        set => SetProperty(ref _statutBarre, value);
    }

    public string MessageErreur
    {
        get => _messageErreur;
        set => SetProperty(ref _messageErreur, value);
    }

    public DateTime DatePresence
    {
        get => _datePresence;
        set
        {
            if (SetProperty(ref _datePresence, value.Date))
                ChargerPresenceJour();
        }
    }

    public int MoisRapport
    {
        get => _moisRapport;
        set => SetProperty(ref _moisRapport, value);
    }

    public int AnneeRapport
    {
        get => _anneeRapport;
        set => SetProperty(ref _anneeRapport, value);
    }

    public Employe? EmployeSelectionne
    {
        get => _employeSelectionne;
        set => SetProperty(ref _employeSelectionne, value);
    }

    public Employe? EmployeEdition
    {
        get => _employeEdition;
        set => SetProperty(ref _employeEdition, value);
    }

    public JourPresenceLigne? LigneJourSelectionnee
    {
        get => _ligneJourSelectionnee;
        set => SetProperty(ref _ligneJourSelectionnee, value);
    }

    public string FiltreEmploye
    {
        get => _filtreEmploye;
        set
        {
            if (SetProperty(ref _filtreEmploye, value))
                ChargerEmployes();
        }
    }

    public int CompteurPresents { get; private set; }
    public int CompteurAbsents { get; private set; }
    public int CompteurEnCours { get; private set; }
    public int CompteurEmployesActifs { get; private set; }

    public string NomEntreprise { get => _nomEntreprise; set => SetProperty(ref _nomEntreprise, value); }
    public string ZkIp { get => _zkIp; set => SetProperty(ref _zkIp, value); }
    public string ZkPort { get => _zkPort; set => SetProperty(ref _zkPort, value); }
    public string ZkMachine { get => _zkMachine; set => SetProperty(ref _zkMachine, value); }
    public string ZkCommPwd { get => _zkCommPwd; set => SetProperty(ref _zkCommPwd, value); }
    public bool ZkSyncActif { get => _zkSyncActif; set => SetProperty(ref _zkSyncActif, value); }
    public string ZkIntervalle { get => _zkIntervalle; set => SetProperty(ref _zkIntervalle, value); }
    public string ZkDerniereSync { get => _zkDerniereSync; set => SetProperty(ref _zkDerniereSync, value); }

    public ICommand NaviguerCommand { get; }
    public ICommand RafraichirCommand { get; }
    public ICommand NouvelEmployeCommand { get; }
    public ICommand EditerEmployeCommand { get; }
    public ICommand SauverEmployeCommand { get; }
    public ICommand SupprimerEmployeCommand { get; }
    public ICommand PointerEntreeCommand { get; }
    public ICommand PointerSortieCommand { get; }
    public ICommand SynchroniserZkCommand { get; }
    public ICommand SauverParametresCommand { get; }
    public ICommand ChargerRapportCommand { get; }
    public ICommand ExportExcelJourCommand { get; }
    public ICommand ExportPdfJourCommand { get; }
    public ICommand ExportExcelMoisCommand { get; }
    public ICommand ExportPdfMoisCommand { get; }

    public void RafraichirTout()
    {
        ChargerEmployes();
        ChargerPresenceJour();
        ChargerAccueil();
        ChargerParametres();
        StatutBarre = "Données actualisées";
    }

    private void ChargerEmployes()
    {
        using var db = new PresenceDbContext();
        var q = db.Employes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(FiltreEmploye))
        {
            var f = FiltreEmploye.Trim();
            q = q.Where(e => e.Nom.Contains(f) || e.Prenom.Contains(f) || e.Matricule.Contains(f) ||
                             (e.CodePinZk != null && e.CodePinZk.Contains(f)));
        }

        var list = q.OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();
        Employes.Clear();
        foreach (var e in list) Employes.Add(e);
        CompteurEmployesActifs = list.Count(e => e.Actif);
        OnPropertyChanged(nameof(CompteurEmployesActifs));
    }

    private void ChargerPresenceJour()
    {
        using var db = new PresenceDbContext();
        var employes = db.Employes.AsNoTracking().ToList();
        var debut = DatePresence.Date;
        var fin = debut.AddDays(1);
        var pts = db.Pointages.AsNoTracking()
            .Where(p => p.Horodatage >= debut && p.Horodatage < fin)
            .ToList();
        var lignes = PresenceCalculService.CalculerJourPourTous(employes, pts, DatePresence);
        PresenceJour.Clear();
        foreach (var l in lignes) PresenceJour.Add(l);

        CompteurPresents = lignes.Count(x => x.Statut == "Présent");
        CompteurEnCours = lignes.Count(x => x.Statut == "En cours");
        CompteurAbsents = lignes.Count(x => x.Statut == "Absent");
        OnPropertyChanged(nameof(CompteurPresents));
        OnPropertyChanged(nameof(CompteurEnCours));
        OnPropertyChanged(nameof(CompteurAbsents));
    }

    private void ChargerAccueil()
    {
        ChargerPresenceJour();
        using var db = new PresenceDbContext();
        var recents = db.Pointages.AsNoTracking()
            .Include(p => p.Employe)
            .OrderByDescending(p => p.Horodatage)
            .Take(15)
            .ToList();
        PointagesRecents.Clear();
        foreach (var p in recents) PointagesRecents.Add(p);
    }

    private void ChargerRapportMois()
    {
        using var db = new PresenceDbContext();
        var employes = db.Employes.AsNoTracking().ToList();
        var debut = new DateTime(AnneeRapport, MoisRapport, 1);
        var fin = debut.AddMonths(1);
        var pts = db.Pointages.AsNoTracking()
            .Where(p => p.Horodatage >= debut && p.Horodatage < fin)
            .ToList();
        var resume = PresenceCalculService.ResumeMensuel(employes, pts, AnneeRapport, MoisRapport);
        ResumeMois.Clear();
        foreach (var (emp, jours, heures, abs) in resume)
        {
            ResumeMois.Add(new ResumeMoisItem
            {
                Matricule = emp.Matricule,
                NomComplet = emp.NomComplet,
                JoursPresents = jours,
                HeuresTotales = heures,
                Absences = abs
            });
        }
    }

    private void NouvelEmploye()
    {
        EmployeEdition = new Employe { Actif = true, Matricule = "", Nom = "", Prenom = "" };
        Onglet = "Employés";
    }

    private void EditerEmploye()
    {
        if (EmployeSelectionne == null) return;
        EmployeEdition = new Employe
        {
            Id = EmployeSelectionne.Id,
            Matricule = EmployeSelectionne.Matricule,
            Nom = EmployeSelectionne.Nom,
            Prenom = EmployeSelectionne.Prenom,
            CodePinZk = EmployeSelectionne.CodePinZk,
            Actif = EmployeSelectionne.Actif
        };
    }

    private void SauverEmploye()
    {
        if (EmployeEdition == null) return;
        if (string.IsNullOrWhiteSpace(EmployeEdition.Matricule) || string.IsNullOrWhiteSpace(EmployeEdition.Nom))
        {
            MessageBox.Show("Matricule et nom sont obligatoires.", "Melody Présence");
            return;
        }

        try
        {
            using var db = new PresenceDbContext();
            if (EmployeEdition.Id == 0)
            {
                if (db.Employes.Any(e => e.Matricule == EmployeEdition.Matricule.Trim()))
                {
                    MessageBox.Show("Ce matricule existe déjà.", "Melody Présence");
                    return;
                }

                db.Employes.Add(new Employe
                {
                    Matricule = EmployeEdition.Matricule.Trim(),
                    Nom = EmployeEdition.Nom.Trim(),
                    Prenom = EmployeEdition.Prenom?.Trim() ?? "",
                    CodePinZk = string.IsNullOrWhiteSpace(EmployeEdition.CodePinZk) ? null : EmployeEdition.CodePinZk.Trim(),
                    Actif = EmployeEdition.Actif
                });
            }
            else
            {
                var e = db.Employes.First(x => x.Id == EmployeEdition.Id);
                if (db.Employes.Any(x => x.Matricule == EmployeEdition.Matricule.Trim() && x.Id != e.Id))
                {
                    MessageBox.Show("Ce matricule existe déjà.", "Melody Présence");
                    return;
                }

                e.Matricule = EmployeEdition.Matricule.Trim();
                e.Nom = EmployeEdition.Nom.Trim();
                e.Prenom = EmployeEdition.Prenom?.Trim() ?? "";
                e.CodePinZk = string.IsNullOrWhiteSpace(EmployeEdition.CodePinZk) ? null : EmployeEdition.CodePinZk.Trim();
                e.Actif = EmployeEdition.Actif;
            }

            db.SaveChanges();
            EmployeEdition = null;
            ChargerEmployes();
            StatutBarre = "Employé enregistré";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Erreur");
        }
    }

    private void SupprimerEmploye()
    {
        if (EmployeSelectionne == null) return;
        if (MessageBox.Show($"Supprimer {EmployeSelectionne.NomComplet} ?", "Confirmation",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        using var db = new PresenceDbContext();
        var e = db.Employes.FirstOrDefault(x => x.Id == EmployeSelectionne.Id);
        if (e != null)
        {
            db.Employes.Remove(e);
            db.SaveChanges();
        }

        EmployeSelectionne = null;
        EmployeEdition = null;
        ChargerEmployes();
        StatutBarre = "Employé supprimé";
    }

    private void Pointer(PointageType type)
    {
        if (LigneJourSelectionnee == null) return;
        var now = DatePresence.Date == DateTime.Today
            ? DateTime.Now
            : DatePresence.Date.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute);
        new PointageService().EnregistrerManuel(LigneJourSelectionnee.EmployeId, now, type);
        ChargerPresenceJour();
        StatutBarre = type == PointageType.Entree ? "Entrée enregistrée" : "Sortie enregistrée";
    }

    private async Task SynchroniserZkAsync()
    {
        StatutBarre = "Synchronisation pointeuse…";
        var (ok, err, nb) = await ZktecoSynchronisationService.TrySynchroniserAsync();
        if (!ok)
        {
            MessageBox.Show(err ?? "Échec synchronisation", "ZKTeco");
            StatutBarre = err ?? "Échec";
            return;
        }

        ChargerPresenceJour();
        ChargerAccueil();
        ChargerParametres();
        StatutBarre = $"Synchronisation OK — {nb} nouveau(x) pointage(s)";
    }

    private void ChargerParametres()
    {
        using var db = new PresenceDbContext();
        var p = db.Parametres.AsNoTracking().FirstOrDefault(x => x.Id == ParametresApplication.SingletonId)
                ?? new ParametresApplication();
        NomEntreprise = p.NomEntreprise;
        ZkIp = p.ZkTerminalIp ?? "";
        ZkPort = p.ZkTerminalPort > 0 ? p.ZkTerminalPort.ToString() : "4370";
        ZkMachine = p.ZkMachineNumber > 0 ? p.ZkMachineNumber.ToString() : "1";
        ZkCommPwd = p.ZkCommPassword == 0 ? "000000" : p.ZkCommPassword.ToString();
        ZkSyncActif = p.ZkSyncActif;
        ZkIntervalle = (p.ZkIntervalleSecondes <= 0 ? 60 : p.ZkIntervalleSecondes).ToString();
        ZkDerniereSync = p.ZkDerniereSyncUtc.HasValue
            ? p.ZkDerniereSyncUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            : "Jamais";
    }

    private void SauverParametres()
    {
        if (!int.TryParse(ZkPort.Trim(), out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("Port invalide.", "Paramètres");
            return;
        }

        if (!int.TryParse(ZkMachine.Trim(), out var machine) || machine <= 0)
            machine = 1;
        if (!int.TryParse(ZkIntervalle.Trim(), out var intervalle) || intervalle < 5)
            intervalle = 60;
        var comm = 0;
        var pwdTx = ZkCommPwd.Trim();
        if (pwdTx != "000000" && !int.TryParse(pwdTx, out comm))
        {
            MessageBox.Show("Mot de passe communication invalide (numérique).", "Paramètres");
            return;
        }

        if (ZkSyncActif && string.IsNullOrWhiteSpace(ZkIp))
        {
            MessageBox.Show("Indiquez l’IP du terminal pour activer la sync auto.", "Paramètres");
            return;
        }

        using var db = new PresenceDbContext();
        var p = db.Parametres.First(x => x.Id == ParametresApplication.SingletonId);
        p.NomEntreprise = string.IsNullOrWhiteSpace(NomEntreprise) ? "Mon entreprise" : NomEntreprise.Trim();
        p.ZkTerminalIp = string.IsNullOrWhiteSpace(ZkIp) ? null : ZkIp.Trim();
        p.ZkTerminalPort = port;
        p.ZkMachineNumber = machine;
        p.ZkCommPassword = comm;
        p.ZkSyncActif = ZkSyncActif;
        p.ZkIntervalleSecondes = intervalle;
        db.SaveChanges();
        ZktecoSynchronisationService.Reconfigurer();
        StatutBarre = "Paramètres enregistrés";
        MessageBox.Show("Paramètres enregistrés.", "Melody Présence");
    }

    private string NomEntrepriseCourant()
    {
        using var db = new PresenceDbContext();
        return db.Parametres.AsNoTracking().FirstOrDefault()?.NomEntreprise ?? "Mon entreprise";
    }

    private void ExporterExcelJour()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Presence_{DatePresence:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        PresenceExportService.ExporterExcelDetailJour(dlg.FileName, NomEntrepriseCourant(), DatePresence, PresenceJour.ToList());
        StatutBarre = "Export Excel jour OK";
    }

    private void ExporterPdfJour()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Presence_{DatePresence:yyyyMMdd}.pdf"
        };
        if (dlg.ShowDialog() != true) return;
        PresenceExportService.ExporterPdfDetailJour(dlg.FileName, NomEntrepriseCourant(), DatePresence, PresenceJour.ToList());
        StatutBarre = "Export PDF jour OK";
    }

    private void ExporterExcelMois()
    {
        ChargerRapportMois();
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Presence_{AnneeRapport}{MoisRapport:D2}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        var data = ResumeMois.Select(r => (
            new Employe { Matricule = r.Matricule, Nom = r.NomComplet },
            r.JoursPresents,
            r.HeuresTotales,
            r.Absences)).ToList();
        PresenceExportService.ExporterExcelResumeMois(dlg.FileName, NomEntrepriseCourant(), AnneeRapport, MoisRapport, data);
        StatutBarre = "Export Excel mois OK";
    }

    private void ExporterPdfMois()
    {
        ChargerRapportMois();
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Presence_{AnneeRapport}{MoisRapport:D2}.pdf"
        };
        if (dlg.ShowDialog() != true) return;
        var data = ResumeMois.Select(r => (
            new Employe { Matricule = r.Matricule, Nom = r.NomComplet },
            r.JoursPresents,
            r.HeuresTotales,
            r.Absences)).ToList();
        PresenceExportService.ExporterPdfResumeMois(dlg.FileName, NomEntrepriseCourant(), AnneeRapport, MoisRapport, data);
        StatutBarre = "Export PDF mois OK";
    }
}

public class ResumeMoisItem
{
    public string Matricule { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public int JoursPresents { get; set; }
    public double HeuresTotales { get; set; }
    public int Absences { get; set; }
}
