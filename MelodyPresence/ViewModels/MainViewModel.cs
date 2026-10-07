using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private DateTime _derniereActualisationLocale = DateTime.Now;
    private bool _statutEstErreur;
    private string _messageErreur = "";
    private DateTime _datePresence = DateTime.Today;
    private int _moisRapport = PeriodePaieLtService.PeriodeCourante().MoisPaiement;
    private int _anneeRapport = PeriodePaieLtService.PeriodeCourante().AnneePaiement;
    private Employe? _employeSelectionne;
    private Employe? _employeEdition;
    private JourPresenceLigne? _ligneJourSelectionnee;
    private string _filtreEmploye = "";
    private string _filtrePresenceTexte = "";
    private string _filtrePresenceStatut = "Tous";
    private string _editionEntree = PresenceCalculService.FormatHhMm(PresenceCalculService.HeureDebutDefaut);
    private string _editionSortie = PresenceCalculService.FormatHhMm(PresenceCalculService.HeureFinDefaut);
    private string _nouveauPointageHeure = "";
    private Pointage? _pointageDetailSelectionne;
    private List<JourPresenceLigne> _presenceJourBrute = new();

    // Paramètres
    private string _nomEntreprise = "";
    private string _zkIp = "";
    private string _zkPort = "4370";
    private string _zkMachine = "1";
    private string _zkCommPwd = "000000";
    private bool _zkSyncActif;
    private string _zkIntervalle = "60";
    private string _zkDerniereSync = "Jamais";
    private bool _notificationsWindowsActives = true;
    private bool _demarrerAvecWindows = true;
    private string _heureDebut = PresenceCalculService.FormatHhMm(PresenceCalculService.HeureDebutDefaut);
    private string _heureLimite = PresenceCalculService.FormatHhMm(PresenceCalculService.HeureLimiteDefaut);
    private string _heureFin = PresenceCalculService.FormatHhMm(PresenceCalculService.HeureFinDefaut);
    private bool _presenceListeAgrandie;
    private bool _syncEnCours;
    private string _syncErreur = "";
    private DateTime? _derniereSyncUtc;
    private readonly DispatcherTimer _horlogeTimer;
    private const string HeureDebutPauseDefaut = "12:00";
    private const string HeureFinPauseDefaut = "13:00";

    public MainViewModel()
    {
        Employes = new ObservableCollection<Employe>();
        PresenceJour = new ObservableCollection<JourPresenceLigne>();
        ResumeMois = new ObservableCollection<ResumeMoisItem>();
        PointagesRecents = new ObservableCollection<Pointage>();
        PointagesDetail = new ObservableCollection<Pointage>();

        NaviguerCommand = new RelayCommand(p => Onglet = p?.ToString() ?? "Accueil");
        RafraichirCommand = new RelayCommand(_ => RafraichirTout());
        NouvelEmployeCommand = new RelayCommand(_ => NouvelEmploye());
        EditerEmployeCommand = new RelayCommand(_ => EditerEmploye(), _ => EmployeSelectionne != null);
        SauverEmployeCommand = new RelayCommand(_ => SauverEmploye(), _ => EmployeEdition != null);
        SupprimerEmployeCommand = new RelayCommand(_ => SupprimerEmploye(), _ => EmployeSelectionne != null);
        PointerEntreeCommand = new RelayCommand(_ => Pointer(PointageType.Entree), _ => DetailPeutEntree);
        PointerSortieCommand = new RelayCommand(_ => Pointer(PointageType.Sortie), _ => DetailPeutSortie);
        SynchroniserZkCommand = new RelayCommand(async _ => await SynchroniserZkAsync());
        SauverParametresCommand = new RelayCommand(_ => SauverParametres());
        ChargerRapportCommand = new RelayCommand(_ => ChargerRapportMois());
        ExportExcelJourCommand = new RelayCommand(_ => ExporterExcelJour());
        ExportPdfJourCommand = new RelayCommand(_ => ExporterPdfJour());
        ExportExcelMoisCommand = new RelayCommand(_ => ExporterExcelMois());
        ExportPdfMoisCommand = new RelayCommand(_ => ExporterPdfMois());

        JourPrecedentCommand = new RelayCommand(_ => DatePresence = DatePresence.AddDays(-1));
        JourSuivantCommand = new RelayCommand(_ => DatePresence = DatePresence.AddDays(1));
        JourAujourdhuiCommand = new RelayCommand(_ => DatePresence = DateTime.Today);
        FiltrerStatutCommand = new RelayCommand(p =>
        {
            var s = p?.ToString() ?? "Tous";
            if (s == "AbsentsAuto")
                s = AvantHeureLimite ? "Non pointé" : "Absent";
            if (s == "PrésentsAuto")
                s = "Parti";
            FiltrePresenceStatut = s;
        });
        OuvrirPresenceFiltreCommand = new RelayCommand(p =>
        {
            var s = p?.ToString() ?? "Tous";
            if (s == "AbsentsAuto")
                s = AvantHeureLimite ? "Non pointé" : "Absent";
            if (s == "PrésentsAuto")
                s = "Parti";
            if (s == "RetardsAuto")
                s = "Retard";
            FiltrePresenceStatut = s;
            Onglet = "Présence";
        });
        AppliquerHorairesCommand = new RelayCommand(_ => AppliquerHorairesJournee(), _ => LigneJourSelectionnee != null);
        PresenceStandardCommand = new RelayCommand(_ => AppliquerPresenceStandard(), _ => LigneJourSelectionnee != null);
        AjouterPointageHeureCommand = new RelayCommand(_ => AjouterPointageHeurePrecise(), _ => LigneJourSelectionnee != null);
        SupprimerDernierPointageCommand = new RelayCommand(_ => SupprimerDernierPointage(), _ => LigneJourSelectionnee != null);
        SupprimerJourneeCommand = new RelayCommand(_ => SupprimerJournee(), _ => LigneJourSelectionnee != null);
        SupprimerPointageDetailCommand = new RelayCommand(_ => SupprimerPointageDetail(), _ => PointageDetailSelectionne != null);
        MarquerAbsentCommand = new RelayCommand(_ => MarquerAbsent(), _ => LigneJourSelectionnee != null);
        BasculerPresenceAgrandieCommand = new RelayCommand(_ => PresenceListeAgrandie = !PresenceListeAgrandie);
        ExportCsvJourCommand = new RelayCommand(_ => ExporterCsvJour());
        ConfigurerTerminalCommand = new RelayCommand(_ => Onglet = "Paramètres");
        TesterNotificationCommand = new RelayCommand(_ =>
        {
            WindowsNotificationService.NotifierApercuDesign();
            StatutBarre = "✓ Aperçu notification Windows envoyé";
        });
        VerifierMiseAJourCommand = new RelayCommand(
            async _ => await VerifierMiseAJourAsync(),
            _ => !MiseAJourEnCours);
        ExporterHeuresVersPaieCommand = new RelayCommand(_ => ExporterHeuresVersPaie());
        ExporterPointagesVersPaieCommand = new RelayCommand(_ => ExporterPointagesVersPaie());
        RafraichirPaieCommand = new RelayCommand(_ => ChargerModulePaie());
        GenererBulletinsCommand = new RelayCommand(async _ => await GenererBulletinsMoisAsync());
        ExporterBulletinPdfCommand = new RelayCommand(_ => ExporterBulletinSelectionPdf(), _ => BulletinSelectionne != null);
        ExporterTousBulletinsPdfCommand = new RelayCommand(_ => ExporterTousBulletinsPdf(), _ => BulletinsMois.Count > 0);
        ImporterFicheSalaireCommand = new RelayCommand(_ => ImporterFicheSalaire());
        ImporterEmployesCsvCommand = new RelayCommand(_ => ImporterEmployesCsv());
        RechargerEmployesSeedCommand = new RelayCommand(_ => RechargerEmployesSeed());
        DeconnecterCommand = new RelayCommand(_ => Deconnecter());
        RecalculerTauxBaseCommand = new RelayCommand(_ => RecalculerTauxBaseEmploye(), _ => EmployeEdition != null);

        ZktecoSynchronisationService.SynchroReussie += (utc, nb) =>
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _syncErreur = "";
                RafraichirTout();
                ChargerModulePaie();
                ActualiserEtatSync();
                StatutBarre = nb > 0
                    ? $"✓ Synchronisation terminée — {nb} nouveau(x) pointage(s)"
                    : "✓ Synchronisation terminée";
                WindowsNotificationService.NotifierSyncSucces(nb);
            });
        ZktecoSynchronisationService.SynchroErreur += err =>
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _syncErreur = err ?? "Synchronisation impossible";
                ActualiserEtatSync();
                StatutBarre = _syncErreur;
                WindowsNotificationService.NotifierSyncErreur(_syncErreur);
            });

        RafraichirTout();
        ChargerParametres();
        EditionEntree = HeureDebutTravail;
        EditionSortie = HeureFinTravail;
        ZktecoSynchronisationService.Reconfigurer();

        _horlogeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _horlogeTimer.Tick += (_, _) => NotifierHorlogeDashboard();
        _horlogeTimer.Start();
    }

    private TimeSpan DebutTravailTs =>
        PresenceCalculService.ParserHeure(HeureDebutTravail, PresenceCalculService.HeureDebutDefaut);

    private TimeSpan LimiteToleranceTs =>
        PresenceCalculService.ParserHeure(HeureLimiteTolerance, PresenceCalculService.HeureLimiteDefaut);

    private TimeSpan FinTravailTs =>
        PresenceCalculService.ParserHeure(HeureFinTravail, PresenceCalculService.HeureFinDefaut);

    public ObservableCollection<Employe> Employes { get; }
    public ObservableCollection<JourPresenceLigne> PresenceJour { get; }
    public ObservableCollection<ResumeMoisItem> ResumeMois { get; }
    public ObservableCollection<Pointage> PointagesRecents { get; }
    public ObservableCollection<Pointage> PointagesDetail { get; }

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
                OnPropertyChanged(nameof(EstPaie));
                OnPropertyChanged(nameof(EstParametres));
                if (value == "Présence") ChargerPresenceJour();
                if (value == "Rapports") ChargerRapportMois();
                if (value == "Paie") ChargerModulePaie();
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
    public bool EstPaie => Onglet == "Paie";
    public bool EstParametres => Onglet == "Paramètres";

    public string StatutBarre
    {
        get => _statutBarre;
        set
        {
            if (!SetProperty(ref _statutBarre, value)) return;
            _derniereActualisationLocale = DateTime.Now;
            _statutEstErreur = ContientErreurStatut(value);
            OnPropertyChanged(nameof(StatutBarreAffiche));
            OnPropertyChanged(nameof(StatutBarreBrush));
        }
    }

    public string StatutBarreAffiche
    {
        get
        {
            var heure = _derniereActualisationLocale.ToString("HH:mm");
            if (_syncEnCours) return $"Synchronisation en cours · {heure}";
            if (_statutEstErreur || !string.IsNullOrWhiteSpace(_syncErreur) &&
                (_statutBarre?.Contains("échou", StringComparison.OrdinalIgnoreCase) == true ||
                 _statutBarre?.Contains("impossible", StringComparison.OrdinalIgnoreCase) == true ||
                 _statutBarre?.Contains("Échec", StringComparison.OrdinalIgnoreCase) == true ||
                 _statutBarre?.Contains("non configur", StringComparison.OrdinalIgnoreCase) == true))
                return $"{NettoyerPrefixeStatut(_statutBarre)} · {heure}";
            if (string.Equals(_statutBarre, "Données actualisées", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_statutBarre, "Prêt", StringComparison.OrdinalIgnoreCase))
                return $"Données à jour · {heure}";
            return $"{NettoyerPrefixeStatut(_statutBarre)} · {heure}";
        }
    }

    public Brush StatutBarreBrush =>
        _syncEnCours ? new SolidColorBrush(Color.FromRgb(0xFF, 0xC2, 0x33))
        : (_statutEstErreur ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x72))
            : new SolidColorBrush(Color.FromRgb(0x35, 0xD3, 0x9A)));

    private static bool ContientErreurStatut(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("échou", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("Échec", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("impossible", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("Erreur", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("invalide", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("non configur", StringComparison.OrdinalIgnoreCase));

    private static string NettoyerPrefixeStatut(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Données à jour";
        return value.StartsWith("✓ ", StringComparison.Ordinal) ? value[2..] : value;
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
            {
                OnPropertyChanged(nameof(DatePresenceLibelle));
                ChargerPresenceJour();
                NotifierHorlogeDashboard();
            }
        }
    }

    public int MoisRapport
    {
        get => _moisRapport;
        set
        {
            if (SetProperty(ref _moisRapport, value))
            {
                OnPropertyChanged(nameof(PaieResumeHeuresLibelle));
                OnPropertyChanged(nameof(PeriodePaieLibelle));
            }
        }
    }

    public int AnneeRapport
    {
        get => _anneeRapport;
        set
        {
            if (SetProperty(ref _anneeRapport, value))
            {
                OnPropertyChanged(nameof(PaieResumeHeuresLibelle));
                OnPropertyChanged(nameof(PeriodePaieLibelle));
            }
        }
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
        set
        {
            if (SetProperty(ref _ligneJourSelectionnee, value))
            {
                ChargerDetailSelection();
                NotifierDetailUi();
            }
        }
    }

    public bool DetailVisible => LigneJourSelectionnee != null;
    public string DetailTitre => LigneJourSelectionnee == null
        ? ""
        : $"{LigneJourSelectionnee.NomComplet} · {LigneJourSelectionnee.Matricule}";
    public string DetailNom => LigneJourSelectionnee?.NomComplet ?? "";
    public string DetailMatricule => LigneJourSelectionnee?.Matricule ?? "";
    public string DetailStatut => LigneJourSelectionnee?.StatutLibelle ?? "";
    public string DetailEntree => LigneJourSelectionnee?.EntreeAffichee ?? "—";
    public string DetailSortie => LigneJourSelectionnee?.SortieAffichee ?? "—";
    public string DetailHeures => LigneJourSelectionnee == null || LigneJourSelectionnee.NbPointages == 0
        ? "—"
        : JourPresenceLigne.FormatHeures(LigneJourSelectionnee.Heures);
    public string DetailHorairePrevu => $"{HeureDebutTravail} → {HeureFinTravail}";
    public bool DetailPeutEntree => LigneJourSelectionnee?.PeutPointerEntree == true;
    public bool DetailPeutSortie => LigneJourSelectionnee?.PeutPointerSortie == true;
    public bool ListePresenceVide => CompteurAffiches == 0;
    public string MessageListeVide => string.IsNullOrWhiteSpace(FiltrePresenceTexte) && FiltrePresenceStatut == "Tous"
        ? "Aucun employé actif pour cette date."
        : "Aucun résultat pour cette recherche ou ce filtre.";

    public string FiltreEmploye
    {
        get => _filtreEmploye;
        set
        {
            if (SetProperty(ref _filtreEmploye, value))
                ChargerEmployes();
        }
    }

    public string FiltrePresenceTexte
    {
        get => _filtrePresenceTexte;
        set
        {
            if (SetProperty(ref _filtrePresenceTexte, value))
                AppliquerFiltresPresence();
        }
    }

    public string FiltrePresenceStatut
    {
        get => _filtrePresenceStatut;
        set
        {
            if (SetProperty(ref _filtrePresenceStatut, value))
            {
                OnPropertyChanged(nameof(FiltreTousActif));
                OnPropertyChanged(nameof(FiltrePresentsActif));
                OnPropertyChanged(nameof(FiltreRetardsActif));
                OnPropertyChanged(nameof(FiltreEnCoursActif));
                OnPropertyChanged(nameof(FiltreAbsentsActif));
                OnPropertyChanged(nameof(FiltreNonPointesActif));
                OnPropertyChanged(nameof(FiltrePartisActif));
                AppliquerFiltresPresence();
            }
        }
    }

    public bool FiltreTousActif => FiltrePresenceStatut == "Tous";
    public bool FiltrePresentsActif => FiltrePresenceStatut is "Présent" or "Parti";
    public bool FiltreRetardsActif => FiltrePresenceStatut == "Retard";
    public bool FiltreEnCoursActif => FiltrePresenceStatut == "En cours";
    public bool FiltreAbsentsActif => FiltrePresenceStatut is "Absent" or "Non pointé";
    public bool FiltreNonPointesActif => FiltrePresenceStatut == "Non pointé";
    public bool FiltrePartisActif => FiltrePresenceStatut == "Parti";

    public bool PresenceListeAgrandie
    {
        get => _presenceListeAgrandie;
        set
        {
            if (SetProperty(ref _presenceListeAgrandie, value))
            {
                OnPropertyChanged(nameof(PresenceAgrandirIcone));
                OnPropertyChanged(nameof(PresenceAgrandirInfoBulle));
            }
        }
    }

    public string PresenceAgrandirIcone => PresenceListeAgrandie ? "\uE73F" : "\uE740";
    public string PresenceAgrandirInfoBulle => PresenceListeAgrandie ? "Réduire la liste" : "Agrandir la liste";

    public string EditionEntree
    {
        get => _editionEntree;
        set => SetProperty(ref _editionEntree, value);
    }

    public string EditionSortie
    {
        get => _editionSortie;
        set => SetProperty(ref _editionSortie, value);
    }

    public string NouveauPointageHeure
    {
        get => _nouveauPointageHeure;
        set => SetProperty(ref _nouveauPointageHeure, value);
    }

    public Pointage? PointageDetailSelectionne
    {
        get => _pointageDetailSelectionne;
        set => SetProperty(ref _pointageDetailSelectionne, value);
    }

    public string DatePresenceLibelle => DatePresence.ToString("dddd d MMMM yyyy");

    public int CompteurPresents { get; private set; }
    public int CompteurAbsents { get; private set; }
    public int CompteurNonPointes { get; private set; }
    public int CompteurEnCours { get; private set; }
    public int CompteurRetards { get; private set; }
    public int CompteurEmployesActifs { get; private set; }
    public int CompteurAffiches { get; private set; }

    public int CompteurPresencePointee { get; private set; }
    public int CompteurSortis { get; private set; }
    public bool AvantHeureLimite { get; private set; }
    public bool ActiviteRecenteVide => PointagesRecents.Count == 0;
    public bool PresenceJourVide => CompteurPresencePointee == 0;

    public string CompteurPresentsContexte =>
        CompteurEmployesActifs == 0 ? "—" : $"{Pourcent(CompteurPresents, CompteurEmployesActifs)} %";
    public string CompteurRetardsContexte =>
        CompteurEmployesActifs == 0 ? "—" : $"{Pourcent(CompteurRetards, CompteurEmployesActifs)} %";
    public string CompteurEnCoursContexte => "Présents sur site";
    public string CompteurEffectifContexte => "employés";
    public string CompteurAbsentsContexte =>
        CompteurEmployesActifs == 0 ? "—" : $"{Pourcent(CompteurAbsentsOuNonPointes, CompteurEmployesActifs)} %";

    /// <summary>4e KPI : Non pointés avant tolérance, Absents après.</summary>
    public int CompteurAbsentsOuNonPointes => AvantHeureLimite ? CompteurNonPointes : CompteurAbsents;
    public string LabelAbsentsOuNonPointes => AvantHeureLimite ? "NON POINTÉS" : "ABSENTS";
    public string LabelAbsentsOuNonPointesCourt => AvantHeureLimite ? "Non pointés" : "Absents";

    public string PresenceAujourdhuiRatio => $"{CompteurPresencePointee} / {CompteurEmployesActifs}";
    public double PresenceAujourdhuiPourcent =>
        CompteurEmployesActifs <= 0 ? 0 : Math.Round(100.0 * CompteurPresencePointee / CompteurEmployesActifs, 0);
    public string PresenceAujourdhuiPourcentTexte => $"{PresenceAujourdhuiPourcent:0} %";
    public string PresenceAujourdhuiLibelle => CompteurPresencePointee == 0
        ? "Aucun employé n'a encore pointé aujourd'hui."
        : $"{PresenceAujourdhuiPourcent:0} % des employés ont pointé";
    public string DateAujourdhuiCourt => DateTime.Today.ToString("dd/MM/yyyy");
    public string DateAujourdhuiLibelle => $"Aujourd'hui {DateTime.Today:dd/MM/yyyy}";
    public string DatePresenceHeader => DatePresence.ToString("dddd d MMMM yyyy");

    public string HeureActuelle => DateTime.Now.ToString("HH:mm");
    public string DateCourteActuelle => DateTime.Now.ToString("dddd d MMMM");
    public string HeureDebutPause => HeureDebutPauseDefaut;
    public string HeureFinPause => HeureFinPauseDefaut;

    public string JourneeBadge
    {
        get
        {
            var now = DateTime.Now.TimeOfDay;
            var debut = DebutTravailTs;
            var fin = FinTravailTs;
            if (now < debut) return "À venir";
            if (now > fin) return "Terminée";
            return "En cours";
        }
    }

    public string TempsEcouleLibelle
    {
        get
        {
            var debut = DebutTravailTs;
            var now = DateTime.Now.TimeOfDay;
            if (now <= debut) return "00h 00min";
            var ts = now - debut;
            return $"{(int)ts.TotalHours:00}h {ts.Minutes:00}min";
        }
    }

    public string TempsRestantLibelle
    {
        get
        {
            var fin = FinTravailTs;
            var now = DateTime.Now.TimeOfDay;
            if (now >= fin) return "00h 00min";
            var debut = DebutTravailTs;
            if (now < debut) now = debut;
            var ts = fin - now;
            return $"{(int)ts.TotalHours:00}h {ts.Minutes:00}min";
        }
    }

    public double TimelineProgressPercent
    {
        get
        {
            var debut = DebutTravailTs;
            var fin = FinTravailTs;
            if (fin <= debut) return 0;

            // Journée passée / future selon la date affichée
            if (DatePresence.Date < DateTime.Today) return 100;
            if (DatePresence.Date > DateTime.Today) return 0;

            var now = DateTime.Now.TimeOfDay;
            if (now <= debut) return 0;
            if (now >= fin) return 100;
            return Math.Clamp(100.0 * (now - debut).TotalMinutes / (fin - debut).TotalMinutes, 0, 100);
        }
    }

    public string TimelineProgressLibelle =>
        $"{TimelineProgressPercent:0} % · {HeureActuelle}";

    public string TimelineHorlogeLibelle
    {
        get
        {
            var p = TimelineProgressPercent;
            if (DatePresence.Date < DateTime.Today)
                return "Journée terminée — progression 100 %.";
            if (DatePresence.Date > DateTime.Today)
                return "Journée à venir — progression 0 %.";
            if (p <= 0)
                return $"Avant le début ({HeureDebutTravail}) — progression 0 %.";
            if (p >= 100)
                return $"Journée terminée ({HeureFinTravail}) — progression 100 %.";
            return $"Progression réelle {p:0}% · écoulé {TempsEcouleLibelle} · restant {TempsRestantLibelle}";
        }
    }

    public string ZkPortSousTitre => $"Connexion réseau locale · port {ZkPort}";

    public GridLength TimelineProgressStar =>
        TimelineProgressPercent <= 0
            ? new GridLength(0)
            : new GridLength(TimelineProgressPercent, GridUnitType.Star);

    public GridLength TimelineResteStar =>
        TimelineProgressPercent >= 100
            ? new GridLength(0)
            : new GridLength(Math.Max(100 - TimelineProgressPercent, 0.001), GridUnitType.Star);

    /// <summary>Segments proportionnels pour aligner Pause/Reprise sur la vraie échelle horaire.</summary>
    public GridLength TimelineSegDebutPauseStar => TimelineSegmentStar(DebutTravailTs, PauseDebutTs);

    public GridLength TimelineSegPauseRepriseStar => TimelineSegmentStar(PauseDebutTs, PauseFinTs);

    public GridLength TimelineSegRepriseFinStar => TimelineSegmentStar(PauseFinTs, FinTravailTs);

    private TimeSpan PauseDebutTs =>
        PresenceCalculService.ParserHeure(HeureDebutPause, new TimeSpan(12, 0, 0));

    private TimeSpan PauseFinTs =>
        PresenceCalculService.ParserHeure(HeureFinPause, new TimeSpan(13, 0, 0));

    private GridLength TimelineSegmentStar(TimeSpan from, TimeSpan to)
    {
        var debut = DebutTravailTs;
        var fin = FinTravailTs;
        if (fin <= debut) return new GridLength(1, GridUnitType.Star);

        var a = TimeSpan.FromMinutes(Math.Clamp(from.TotalMinutes, debut.TotalMinutes, fin.TotalMinutes));
        var b = TimeSpan.FromMinutes(Math.Clamp(to.TotalMinutes, debut.TotalMinutes, fin.TotalMinutes));
        var minutes = Math.Max((b - a).TotalMinutes, 0.01);
        return new GridLength(minutes, GridUnitType.Star);
    }

    public DoubleCollection PresenceDonutDash
    {
        get
        {
            const double radius = 42;
            const double thickness = 9;
            var c = 2 * Math.PI * radius / thickness;
            var filled = Math.Max(0.001, c * PresenceAujourdhuiPourcent / 100.0);
            return new DoubleCollection { filled, c };
        }
    }

    public string NomUtilisateurAffiche =>
        AuthService.UtilisateurCourant?.NomComplet
        ?? AuthService.UtilisateurCourant?.Identifiant
        ?? "Utilisateur";

    public string RoleUtilisateurAffiche =>
        AuthService.UtilisateurCourant?.Role ?? "—";

    public string InitialesUtilisateur
    {
        get
        {
            var n = NomUtilisateurAffiche.Trim();
            if (string.IsNullOrEmpty(n)) return "?";
            var parts = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }
    }

    public string TerminalCarteTitre => TerminalConfigure ? "Terminal connecté" : "Terminal non configuré";
    public string TerminalCarteDetail => TerminalConfigure ? "Prêt pour le pointage." : "Configuration requise";
    public Brush TerminalCarteBrush => TerminalConfigure
        ? new SolidColorBrush(Color.FromRgb(0x35, 0xD3, 0x9A))
        : new SolidColorBrush(Color.FromRgb(0xFF, 0xC2, 0x33));

    public string PiedSyncDetail =>
        !_derniereSyncUtc.HasValue
            ? "Dernière synchronisation : jamais"
            : $"Dernière synchronisation : {SyncStatutDetail.ToLowerInvariant()}";

    private static int Pourcent(int part, int total) =>
        total <= 0 ? 0 : (int)Math.Round(100.0 * part / total);

    public bool TerminalConfigure => !string.IsNullOrWhiteSpace(ZkIp);
    public bool TerminalAlerteVisible => !TerminalConfigure;
    public string SyncStatutTitre
    {
        get
        {
            if (_syncEnCours) return "Synchronisation en cours…";
            if (!string.IsNullOrWhiteSpace(_syncErreur)) return "Synchronisation impossible";
            if (!_derniereSyncUtc.HasValue) return "Jamais synchronisé";
            return "Synchronisé";
        }
    }

    public string SyncStatutDetail
    {
        get
        {
            if (_syncEnCours) return "Récupération des pointages…";
            if (!string.IsNullOrWhiteSpace(_syncErreur)) return _syncErreur;
            if (!_derniereSyncUtc.HasValue) return "Configurez puis lancez une sync";
            var local = _derniereSyncUtc.Value.ToLocalTime();
            var age = DateTime.Now - local;
            if (age.TotalMinutes < 1) return "À l’instant";
            if (age.TotalMinutes < 60) return $"Il y a {(int)age.TotalMinutes} min";
            if (age.TotalHours < 24) return $"Il y a {(int)age.TotalHours} h";
            return local.ToString("dd/MM HH:mm");
        }
    }

    public Brush SyncStatutBrush
    {
        get
        {
            if (_syncEnCours) return new SolidColorBrush(Color.FromRgb(0xFF, 0xC2, 0x33));
            if (!string.IsNullOrWhiteSpace(_syncErreur) || !_derniereSyncUtc.HasValue)
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x72));
            return new SolidColorBrush(Color.FromRgb(0x35, 0xD3, 0x9A));
        }
    }

    public string NomEntreprise { get => _nomEntreprise; set => SetProperty(ref _nomEntreprise, value); }
    public string ZkIp { get => _zkIp; set => SetProperty(ref _zkIp, value); }
    public string ZkPort
    {
        get => _zkPort;
        set
        {
            if (SetProperty(ref _zkPort, value))
                OnPropertyChanged(nameof(ZkPortSousTitre));
        }
    }
    public string ZkMachine { get => _zkMachine; set => SetProperty(ref _zkMachine, value); }
    public string ZkCommPwd { get => _zkCommPwd; set => SetProperty(ref _zkCommPwd, value); }
    public bool ZkSyncActif { get => _zkSyncActif; set => SetProperty(ref _zkSyncActif, value); }
    public string ZkIntervalle { get => _zkIntervalle; set => SetProperty(ref _zkIntervalle, value); }
    public string ZkDerniereSync { get => _zkDerniereSync; set => SetProperty(ref _zkDerniereSync, value); }

    public bool NotificationsWindowsActives
    {
        get => _notificationsWindowsActives;
        set => SetProperty(ref _notificationsWindowsActives, value);
    }

    public bool DemarrerAvecWindows
    {
        get => _demarrerAvecWindows;
        set => SetProperty(ref _demarrerAvecWindows, value);
    }
    public string HeureDebutTravail
    {
        get => _heureDebut;
        set
        {
            if (SetProperty(ref _heureDebut, value))
            {
                OnPropertyChanged(nameof(DetailHorairePrevu));
                NotifierHorlogeDashboard();
            }
        }
    }

    public string HeureLimiteTolerance { get => _heureLimite; set => SetProperty(ref _heureLimite, value); }

    public string HeureFinTravail
    {
        get => _heureFin;
        set
        {
            if (SetProperty(ref _heureFin, value))
            {
                OnPropertyChanged(nameof(DetailHorairePrevu));
                NotifierHorlogeDashboard();
            }
        }
    }

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
    public ICommand JourPrecedentCommand { get; }
    public ICommand JourSuivantCommand { get; }
    public ICommand JourAujourdhuiCommand { get; }
    public ICommand FiltrerStatutCommand { get; }
    public ICommand OuvrirPresenceFiltreCommand { get; }
    public ICommand AppliquerHorairesCommand { get; }
    public ICommand PresenceStandardCommand { get; }
    public ICommand AjouterPointageHeureCommand { get; }
    public ICommand SupprimerDernierPointageCommand { get; }
    public ICommand SupprimerJourneeCommand { get; }
    public ICommand SupprimerPointageDetailCommand { get; }
    public ICommand MarquerAbsentCommand { get; }
    public ICommand BasculerPresenceAgrandieCommand { get; }
    public ICommand ExportCsvJourCommand { get; }
    public ICommand ConfigurerTerminalCommand { get; }
    public ICommand TesterNotificationCommand { get; }
    public ICommand VerifierMiseAJourCommand { get; }
    public ICommand ExporterHeuresVersPaieCommand { get; }
    public ICommand ExporterPointagesVersPaieCommand { get; }
    public ICommand RafraichirPaieCommand { get; }
    public ICommand GenererBulletinsCommand { get; }
    public ICommand ExporterBulletinPdfCommand { get; }
    public ICommand ExporterTousBulletinsPdfCommand { get; }
    public ICommand ImporterFicheSalaireCommand { get; }
    public ICommand ImporterEmployesCsvCommand { get; }
    public ICommand RechargerEmployesSeedCommand { get; }
    public ICommand DeconnecterCommand { get; }
    public ICommand RecalculerTauxBaseCommand { get; }

    public ObservableCollection<BulletinPaie> BulletinsMois { get; } = new();

    private BulletinPaie? _bulletinSelectionne;
    public BulletinPaie? BulletinSelectionne
    {
        get => _bulletinSelectionne;
        set => SetProperty(ref _bulletinSelectionne, value);
    }

    public string PaieRegleRetards => RetardSanctionService.LibelleRegle;

    public string PaieResumeHeuresLibelle
    {
        get
        {
            var b = PeriodePaieLtService.ObtenirBornes(AnneeRapport, MoisRapport);
            return $"{ResumeMois.Count} employé(s) · {ResumeMois.Sum(x => x.HeuresTotales):0.##} h · {PeriodePaieLtService.LibelleCourt(b)}";
        }
    }

    public string PeriodePaieLibelle =>
        PeriodePaieLtService.LibelleSituation(PeriodePaieLtService.ObtenirBornes(AnneeRapport, MoisRapport));

    public string BulletinsResumeLibelle =>
        $"{BulletinsMois.Count} bulletin(s) · Net total {BulletinsMois.Sum(b => b.NetAPayer):N2}";

    public string VersionApplication =>
        ApplicationUpdateService.FormaterVersion(ApplicationUpdateService.ObtenirVersionInstallee());

    private bool _miseAJourEnCours;
    public bool MiseAJourEnCours
    {
        get => _miseAJourEnCours;
        set
        {
            if (SetProperty(ref _miseAJourEnCours, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _messageMiseAJour = "Vérifiez périodiquement les nouvelles versions LT Présence.";
    public string MessageMiseAJour
    {
        get => _messageMiseAJour;
        set => SetProperty(ref _messageMiseAJour, value);
    }

    public void RafraichirTout()
    {
        ChargerEmployes();
        ChargerPresenceJour();
        ChargerAccueil();
        ChargerParametres();
        if (EstRapports || EstPaie)
            ChargerModulePaie();
        ActualiserEtatSync();
        StatutBarre = "Données à jour";
    }

    private void NotifierDetailUi()
    {
        OnPropertyChanged(nameof(DetailVisible));
        OnPropertyChanged(nameof(DetailTitre));
        OnPropertyChanged(nameof(DetailNom));
        OnPropertyChanged(nameof(DetailMatricule));
        OnPropertyChanged(nameof(DetailStatut));
        OnPropertyChanged(nameof(DetailEntree));
        OnPropertyChanged(nameof(DetailSortie));
        OnPropertyChanged(nameof(DetailHeures));
        OnPropertyChanged(nameof(DetailHorairePrevu));
        OnPropertyChanged(nameof(DetailPeutEntree));
        OnPropertyChanged(nameof(DetailPeutSortie));
        (PointerEntreeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PointerSortieCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AppliquerHorairesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ActualiserEtatSync()
    {
        OnPropertyChanged(nameof(SyncStatutTitre));
        OnPropertyChanged(nameof(SyncStatutDetail));
        OnPropertyChanged(nameof(SyncStatutBrush));
        OnPropertyChanged(nameof(TerminalConfigure));
        OnPropertyChanged(nameof(TerminalAlerteVisible));
        OnPropertyChanged(nameof(TerminalCarteTitre));
        OnPropertyChanged(nameof(TerminalCarteDetail));
        OnPropertyChanged(nameof(TerminalCarteBrush));
        OnPropertyChanged(nameof(PiedSyncDetail));
        OnPropertyChanged(nameof(StatutBarreAffiche));
        OnPropertyChanged(nameof(StatutBarreBrush));
    }

    private void NotifierHorlogeDashboard()
    {
        OnPropertyChanged(nameof(HeureActuelle));
        OnPropertyChanged(nameof(DateCourteActuelle));
        OnPropertyChanged(nameof(JourneeBadge));
        OnPropertyChanged(nameof(TempsEcouleLibelle));
        OnPropertyChanged(nameof(TempsRestantLibelle));
        OnPropertyChanged(nameof(TimelineProgressPercent));
        OnPropertyChanged(nameof(TimelineProgressLibelle));
        OnPropertyChanged(nameof(TimelineHorlogeLibelle));
        OnPropertyChanged(nameof(TimelineProgressStar));
        OnPropertyChanged(nameof(TimelineResteStar));
        OnPropertyChanged(nameof(TimelineSegDebutPauseStar));
        OnPropertyChanged(nameof(TimelineSegPauseRepriseStar));
        OnPropertyChanged(nameof(TimelineSegRepriseFinStar));
        OnPropertyChanged(nameof(StatutBarreAffiche));
        OnPropertyChanged(nameof(PiedSyncDetail));
        OnPropertyChanged(nameof(SyncStatutDetail));
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
        var selectedId = LigneJourSelectionnee?.EmployeId;
        using var db = new PresenceDbContext();
        var employes = db.Employes.AsNoTracking().ToList();
        var debut = DatePresence.Date;
        var fin = debut.AddDays(1);
        var pts = db.Pointages.AsNoTracking()
            .Where(p => p.Horodatage >= debut && p.Horodatage < fin)
            .ToList();
        var (hDebut, hLimite) = LireHoraires(db);
        _presenceJourBrute = PresenceCalculService.CalculerJourPourTous(employes, pts, DatePresence, hDebut, hLimite).ToList();

        // Présents = ont pointé à l'heure (en cours ou journée terminée), hors retards.
        CompteurPresents = _presenceJourBrute.Count(x => x.Statut is "Parti" or "Présent" or "En cours");
        CompteurEnCours = _presenceJourBrute.Count(x => x.Statut == "En cours");
        CompteurRetards = _presenceJourBrute.Count(x => x.EstEnRetard);
        CompteurAbsents = _presenceJourBrute.Count(x => x.Statut == "Absent");
        CompteurNonPointes = _presenceJourBrute.Count(x => x.Statut == "Non pointé");
        CompteurPresencePointee = _presenceJourBrute.Count(x => x.NbPointages > 0);
        CompteurSortis = _presenceJourBrute.Count(x => x.Statut == "Parti");
        AvantHeureLimite = DatePresence.Date == DateTime.Today && DateTime.Now.TimeOfDay < hLimite;
        OnPropertyChanged(nameof(CompteurPresents));
        OnPropertyChanged(nameof(CompteurEnCours));
        OnPropertyChanged(nameof(CompteurRetards));
        OnPropertyChanged(nameof(CompteurAbsents));
        OnPropertyChanged(nameof(CompteurNonPointes));
        OnPropertyChanged(nameof(CompteurPresencePointee));
        OnPropertyChanged(nameof(CompteurSortis));
        OnPropertyChanged(nameof(AvantHeureLimite));
        OnPropertyChanged(nameof(CompteurAbsentsOuNonPointes));
        OnPropertyChanged(nameof(LabelAbsentsOuNonPointes));
        OnPropertyChanged(nameof(LabelAbsentsOuNonPointesCourt));
        OnPropertyChanged(nameof(CompteurPresentsContexte));
        OnPropertyChanged(nameof(CompteurRetardsContexte));
        OnPropertyChanged(nameof(CompteurEnCoursContexte));
        OnPropertyChanged(nameof(CompteurEffectifContexte));
        OnPropertyChanged(nameof(CompteurAbsentsContexte));
        OnPropertyChanged(nameof(PresenceAujourdhuiRatio));
        OnPropertyChanged(nameof(PresenceAujourdhuiPourcent));
        OnPropertyChanged(nameof(PresenceAujourdhuiPourcentTexte));
        OnPropertyChanged(nameof(PresenceAujourdhuiLibelle));
        OnPropertyChanged(nameof(PresenceJourVide));
        OnPropertyChanged(nameof(PresenceDonutDash));
        OnPropertyChanged(nameof(DateAujourdhuiCourt));
        OnPropertyChanged(nameof(DateAujourdhuiLibelle));
        OnPropertyChanged(nameof(DatePresenceHeader));
        OnPropertyChanged(nameof(DatePresenceLibelle));
        NotifierHorlogeDashboard();

        AppliquerFiltresPresence();

        if (selectedId.HasValue)
        {
            LigneJourSelectionnee = PresenceJour.FirstOrDefault(x => x.EmployeId == selectedId.Value)
                                    ?? _presenceJourBrute.FirstOrDefault(x => x.EmployeId == selectedId.Value);
        }
        else
        {
            ChargerDetailSelection();
            NotifierDetailUi();
        }
    }

    private void AppliquerFiltresPresence()
    {
        IEnumerable<JourPresenceLigne> q = _presenceJourBrute;
        if (!string.IsNullOrWhiteSpace(FiltrePresenceTexte))
        {
            var f = FiltrePresenceTexte.Trim();
            q = q.Where(x =>
                x.NomComplet.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                x.Matricule.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        q = FiltrePresenceStatut switch
        {
            "Présent" or "Parti" => q.Where(x => x.Statut is "Parti" or "Présent" or "En cours"),
            "Retard" => q.Where(x => x.EstEnRetard),
            "En cours" => q.Where(x => x.Statut == "En cours"),
            "Absent" => q.Where(x => x.Statut == "Absent"),
            "Non pointé" => q.Where(x => x.Statut == "Non pointé"),
            _ => q
        };

        var list = q.ToList();
        PresenceJour.Clear();
        foreach (var l in list) PresenceJour.Add(l);
        CompteurAffiches = list.Count;
        OnPropertyChanged(nameof(CompteurAffiches));
        OnPropertyChanged(nameof(ListePresenceVide));
        OnPropertyChanged(nameof(MessageListeVide));
    }

    private void ChargerDetailSelection()
    {
        PointagesDetail.Clear();
        PointageDetailSelectionne = null;
        if (LigneJourSelectionnee == null)
        {
            EditionEntree = HeureDebutTravail;
            EditionSortie = HeureFinTravail;
            return;
        }

        EditionEntree = LigneJourSelectionnee.PremiereEntree?.ToString("HH:mm") ?? HeureDebutTravail;
        EditionSortie = LigneJourSelectionnee.DerniereSortie?.ToString("HH:mm") ?? HeureFinTravail;
        NouveauPointageHeure = DateTime.Now.ToString("HH:mm");

        var pts = new PointageService().ListerDuJour(LigneJourSelectionnee.EmployeId, DatePresence);
        foreach (var p in pts) PointagesDetail.Add(p);
    }

    private void AppliquerHorairesJournee()
    {
        if (LigneJourSelectionnee == null) return;
        var entree = PresenceCalculService.ParserHeure(EditionEntree, DebutTravailTs);
        TimeSpan? sortie = null;
        if (!string.IsNullOrWhiteSpace(EditionSortie))
            sortie = PresenceCalculService.ParserHeure(EditionSortie, FinTravailTs);

        if (sortie.HasValue && sortie.Value <= entree)
        {
            MessageBox.Show("La sortie doit être après l’entrée.", "Présence");
            return;
        }

        new PointageService().DefinirJournee(LigneJourSelectionnee.EmployeId, DatePresence, entree, sortie);
        ChargerPresenceJour();
        StatutBarre = $"✓ Correction enregistrée pour {LigneJourSelectionnee?.NomComplet}";
    }

    private void AppliquerPresenceStandard()
    {
        if (LigneJourSelectionnee == null) return;
        var debut = DebutTravailTs;
        var fin = FinTravailTs;
        new PointageService().DefinirJournee(LigneJourSelectionnee.EmployeId, DatePresence, debut, fin);
        ChargerPresenceJour();
        StatutBarre = $"Présence standard appliquée ({PresenceCalculService.FormatHhMm(debut)} → {PresenceCalculService.FormatHhMm(fin)})";
    }

    private void AjouterPointageHeurePrecise()
    {
        if (LigneJourSelectionnee == null) return;
        var heure = PresenceCalculService.ParserHeure(
            string.IsNullOrWhiteSpace(NouveauPointageHeure) ? DateTime.Now.ToString("HH:mm") : NouveauPointageHeure,
            DateTime.Now.TimeOfDay);
        var when = DatePresence.Date.Add(heure);
        var type = PointagesDetail.Count % 2 == 0 ? PointageType.Entree : PointageType.Sortie;
        new PointageService().EnregistrerManuel(LigneJourSelectionnee.EmployeId, when, type);
        ChargerPresenceJour();
        StatutBarre = $"Pointage ajouté à {heure:hh\\:mm}";
    }

    private void SupprimerDernierPointage()
    {
        if (LigneJourSelectionnee == null) return;
        if (!new PointageService().SupprimerDernierDuJour(LigneJourSelectionnee.EmployeId, DatePresence))
        {
            MessageBox.Show("Aucun pointage à supprimer.", "Présence");
            return;
        }

        ChargerPresenceJour();
        StatutBarre = "Dernier pointage supprimé";
    }

    private void SupprimerJournee()
    {
        if (LigneJourSelectionnee == null) return;
        if (MessageBox.Show(
                $"Effacer tous les pointages de {LigneJourSelectionnee.NomComplet} le {DatePresence:dd/MM/yyyy} ?",
                "Confirmation", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        var n = new PointageService().SupprimerTousDuJour(LigneJourSelectionnee.EmployeId, DatePresence);
        ChargerPresenceJour();
        StatutBarre = $"{n} pointage(s) effacé(s)";
    }

    private void SupprimerPointageDetail()
    {
        if (PointageDetailSelectionne == null) return;
        new PointageService().Supprimer(PointageDetailSelectionne.Id);
        ChargerPresenceJour();
        StatutBarre = "Pointage supprimé";
    }

    private void MarquerAbsent()
    {
        if (LigneJourSelectionnee == null) return;
        if (MessageBox.Show(
                $"Marquer {LigneJourSelectionnee.NomComplet} absent (effacer les pointages du jour) ?",
                "Confirmation", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        new PointageService().SupprimerTousDuJour(LigneJourSelectionnee.EmployeId, DatePresence);
        ChargerPresenceJour();
        StatutBarre = "Marqué absent";
    }

    private static (TimeSpan Debut, TimeSpan Limite) LireHoraires(PresenceDbContext db)
    {
        var p = db.Parametres.AsNoTracking().FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
        return PresenceCalculService.LireHoraires(p);
    }

    private void ChargerAccueil()
    {
        ChargerPresenceJour();
        using var db = new PresenceDbContext();
        var recents = db.Pointages.AsNoTracking()
            .Include(p => p.Employe)
            .OrderByDescending(p => p.Horodatage)
            .Take(6)
            .ToList();
        PointagesRecents.Clear();
        foreach (var p in recents) PointagesRecents.Add(p);
        OnPropertyChanged(nameof(ActiviteRecenteVide));
    }

    private void ChargerRapportMois()
    {
        using var db = new PresenceDbContext();
        var employes = db.Employes.AsNoTracking().ToList();
        var bornes = PeriodePaieLtService.ObtenirBornes(AnneeRapport, MoisRapport);
        var finEff = PeriodePaieLtService.FinEffective(bornes);
        var pts = finEff < bornes.Debut
            ? []
            : db.Pointages.AsNoTracking()
                .Where(p => p.Horodatage >= bornes.Debut && p.Horodatage < finEff.AddDays(1))
                .ToList();
        var (hDebut, hLimite) = LireHoraires(db);
        var resume = PresenceCalculService.ResumeMensuel(
            employes, pts, AnneeRapport, MoisRapport, hDebut, hLimite, DateTime.Today);
        ResumeMois.Clear();
        foreach (var (emp, jours, heures, abs, retards) in resume)
        {
            ResumeMois.Add(new ResumeMoisItem
            {
                Matricule = emp.Matricule,
                NomComplet = emp.NomComplet,
                JoursPresents = jours,
                HeuresTotales = heures,
                Absences = abs,
                Retards = retards
            });
        }
        OnPropertyChanged(nameof(PaieResumeHeuresLibelle));
        OnPropertyChanged(nameof(PeriodePaieLibelle));
    }

    private void ChargerModulePaie()
    {
        ChargerRapportMois();
        BulletinsMois.Clear();
        foreach (var b in CalculBulletinService.ListerMois(AnneeRapport, MoisRapport))
            BulletinsMois.Add(b);
        OnPropertyChanged(nameof(BulletinsResumeLibelle));
        StatutBarre = "Paie — heures et bulletins du mois";
    }

    private void Deconnecter()
    {
        var ok = MessageBox.Show(
            "Se déconnecter de LT Présence ?",
            "Déconnexion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes)
            return;

        if (Application.Current is App app)
            app.DeconnexionEtRelancerLogin();
    }

    private void ImporterFicheSalaire()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Fiche salaire Excel (*.xlsx)|*.xlsx",
            Title = "Importer FICHER DE SALAIRE (feuille SALAIRE ET TAXE)"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var r = FicheSalaireImportService.Importer(dlg.FileName);
            ChargerEmployes();
            ChargerModulePaie();
            StatutBarre = $"✓ Salaires importés : {r.MisAJour}/{r.LignesLues} (créés : {r.Crees})";
            MessageBox.Show(
                $"Import terminé.\n\nLignes lues : {r.LignesLues}\nSalaires mis à jour : {r.MisAJour}\nEmployés créés : {r.Crees}",
                "Fiche salaire",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import fiche salaire", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImporterEmployesCsv()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CSV employés (*.csv)|*.csv",
            Title = "Importer la liste des employés LT (Matricule;Nom;Postnom;Prenom;CodePinZk)"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var r = EmployeCsvImportService.Importer(dlg.FileName);
            ChargerEmployes();
            ChargerPresenceJour();
            ChargerAccueil();
            StatutBarre = $"✓ Employés : {r.Crees} créés, {r.MisAJour} mis à jour";
            MessageBox.Show(
                $"Import CSV terminé.\n\nLignes : {r.LignesLues}\nCréés : {r.Crees}\nMis à jour : {r.MisAJour}",
                "Employés",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import employés", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RechargerEmployesSeed()
    {
        try
        {
            using var db = new PresenceDbContext();
            var nb = db.Employes.Count();
            if (nb > 0)
            {
                var conf = MessageBox.Show(
                    $"La base contient déjà {nb} employé(s).\n\nAjouter uniquement les matricules manquants depuis le fichier livré avec l'application ?",
                    "Recharger liste LT",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (conf != MessageBoxResult.Yes)
                    return;
            }

            var csv = EmployeCsvImportService.CheminSeedParDefaut();
            if (csv == null)
            {
                MessageBox.Show(
                    "Fichier seed introuvable (Assets/Seed/lt_services_employes.csv).\nUtilisez « Importer CSV ».",
                    "Employés",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var r = EmployeCsvImportService.Importer(csv, creerSeulementSiAbsent: nb > 0);
            var xlsx = FicheSalaireImportService.CheminSeedSalaireParDefaut();
            FicheSalaireImportResult? sal = null;
            if (xlsx != null)
                sal = FicheSalaireImportService.Importer(xlsx);

            ChargerEmployes();
            ChargerPresenceJour();
            ChargerAccueil();
            ChargerModulePaie();
            StatutBarre = $"✓ Liste LT : +{r.Crees} employé(s)" +
                          (sal != null ? $", salaires {sal.MisAJour}" : "");
            MessageBox.Show(
                $"Liste LT rechargée.\n\nCréés : {r.Crees}\nMis à jour : {r.MisAJour}" +
                (sal != null ? $"\nSalaires : {sal.MisAJour} (créés fiche : {sal.Crees})" : ""),
                "Employés LT",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Recharger liste LT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task GenererBulletinsMoisAsync()
    {
        // Synchronisation pointeuse avant calcul réel
        StatutBarre = "Synchronisation pointeuse avant bulletins…";
        var (ok, err, nb) = await ZktecoSynchronisationService.TrySynchroniserAsync();
        if (!ok)
        {
            var cont = MessageBox.Show(
                $"Synchronisation impossible :\n{err}\n\nGénérer quand même avec les pointages déjà en base ?",
                "Bulletins", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (cont != MessageBoxResult.Yes)
            {
                StatutBarre = "Génération annulée — sync échouée";
                return;
            }
        }
        else
        {
            StatutBarre = $"✓ Sync OK ({nb} nouveau(x)) — calcul des bulletins…";
            ChargerPresenceJour();
            ChargerAccueil();
        }

        var sansSalaire = 0;
        using (var db = new PresenceDbContext())
            sansSalaire = db.Employes.Count(e => e.Actif && e.SalaireMensuel <= 0 && e.TauxSalaireBase <= 0);

        if (sansSalaire > 0)
        {
            var okSal = MessageBox.Show(
                $"{sansSalaire} employé(s) actif(s) n’ont pas de salaire / taux A PAYER.\n" +
                "Leurs bulletins auront un net à 0. Continuer ?",
                "Bulletins", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (okSal != MessageBoxResult.Yes)
                return;
        }

        var bornes = PeriodePaieLtService.ObtenirBornes(AnneeRapport, MoisRapport);
        var liste = CalculBulletinService.GenererMois(AnneeRapport, MoisRapport);
        BulletinsMois.Clear();
        foreach (var b in liste)
            BulletinsMois.Add(b);
        OnPropertyChanged(nameof(BulletinsResumeLibelle));
        ChargerRapportMois();
        var situation = PeriodePaieLtService.LibelleSituation(bornes);
        StatutBarre = $"✓ {liste.Count} bulletin(s) réel(s) — {situation}";
        WindowsNotificationService.NotifierInfo(
            $"{liste.Count} bulletin(s) généré(s)\n{situation}",
            "Bulletins LT Présence");
    }

    private void ExporterBulletinSelectionPdf()
    {
        if (BulletinSelectionne == null)
            return;
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"{BulletinSelectionne.Numero}.pdf"
        };
        if (dlg.ShowDialog() != true)
            return;
        BulletinPdfService.Exporter(BulletinSelectionne, NomEntrepriseCourant(), dlg.FileName);
        StatutBarre = "✓ Bulletin PDF exporté";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    private void ExporterTousBulletinsPdf()
    {
        if (BulletinsMois.Count == 0)
        {
            MessageBox.Show("Générez d’abord les bulletins du mois.", "Bulletins");
            return;
        }

        var dlg = new OpenFolderDialog { Title = "Dossier pour les PDF bulletins" };
        if (dlg.ShowDialog() != true)
            return;

        BulletinPdfService.ExporterTous(BulletinsMois, NomEntrepriseCourant(), dlg.FolderName);
        StatutBarre = $"✓ {BulletinsMois.Count} PDF exportés";
        MessageBox.Show($"Bulletins enregistrés dans :\n{dlg.FolderName}", "Bulletins");
    }

    private void ExporterHeuresVersPaie()
    {
        ChargerRapportMois();
        if (ResumeMois.Count == 0)
        {
            MessageBox.Show("Aucune donnée d’heures pour ce mois.", "Export Paie");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CSV Paie (*.csv)|*.csv",
            FileName = $"LT_Presence_Heures_{AnneeRapport}{MoisRapport:D2}.csv"
        };
        if (dlg.ShowDialog() != true)
            return;

        PaieExportService.ExporterHeuresMoisCsv(
            dlg.FileName, NomEntrepriseCourant(), AnneeRapport, MoisRapport, ResumeMois.ToList());
        StatutBarre = "✓ Export heures → Paie terminé";
        MessageBox.Show("Export CSV des heures enregistré.", "Paie", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExporterPointagesVersPaie()
    {
        using var db = new PresenceDbContext();
        var bornes = PeriodePaieLtService.ObtenirBornes(AnneeRapport, MoisRapport);
        var finExclue = bornes.Fin.AddDays(1);
        var employes = db.Employes.AsNoTracking().ToDictionary(e => e.Id);
        var pts = db.Pointages.AsNoTracking()
            .Where(p => p.Horodatage >= bornes.Debut && p.Horodatage < finExclue)
            .ToList();
        if (pts.Count == 0)
        {
            MessageBox.Show("Aucun pointage pour ce mois.", "Export Paie");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CSV Pointages (*.csv)|*.csv",
            FileName = $"LT_Presence_Pointages_{AnneeRapport}{MoisRapport:D2}.csv"
        };
        if (dlg.ShowDialog() != true)
            return;

        PaieExportService.ExporterPointagesCsv(dlg.FileName, pts, employes);
        StatutBarre = "✓ Export pointages → Paie terminé";
    }

    private void NouvelEmploye()
    {
        EmployeEdition = new Employe { Actif = true, Matricule = "", Nom = "", Prenom = "" };
        Onglet = "Employés";
    }

    private void EditerEmploye()
    {
        if (EmployeSelectionne == null) return;
        EmployeEdition = ClonerEmploye(EmployeSelectionne);
    }

    private void SauverEmploye()
    {
        if (EmployeEdition == null) return;
        if (string.IsNullOrWhiteSpace(EmployeEdition.Matricule) || string.IsNullOrWhiteSpace(EmployeEdition.Nom))
        {
            MessageBox.Show("Matricule et nom sont obligatoires.", "LT Services");
            return;
        }

        NormaliserMontantsPaie(EmployeEdition);
        if (EmployeEdition.SalaireMensuel > 0 && EmployeEdition.TauxSalaireBase <= 0)
            RubriquesAPayerLt.CompleterTauxSiBesoin(EmployeEdition);

        try
        {
            using var db = new PresenceDbContext();
            if (EmployeEdition.Id == 0)
            {
                if (db.Employes.Any(e => e.Matricule == EmployeEdition.Matricule.Trim()))
                {
                    MessageBox.Show("Ce matricule existe déjà.", "LT Services");
                    return;
                }

                var nouveau = new Employe();
                AppliquerFicheEmploye(nouveau, EmployeEdition);
                db.Employes.Add(nouveau);
            }
            else
            {
                var e = db.Employes.First(x => x.Id == EmployeEdition.Id);
                if (db.Employes.Any(x => x.Matricule == EmployeEdition.Matricule.Trim() && x.Id != e.Id))
                {
                    MessageBox.Show("Ce matricule existe déjà.", "LT Services");
                    return;
                }

                AppliquerFicheEmploye(e, EmployeEdition);
            }

            db.SaveChanges();
            EmployeEdition = null;
            ChargerEmployes();
            StatutBarre = "Employé et données de paie enregistrés";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Erreur");
        }
    }

    private void RecalculerTauxBaseEmploye()
    {
        if (EmployeEdition == null) return;
        if (EmployeEdition.SalaireMensuel <= 0)
        {
            MessageBox.Show("Indiquez d’abord le salaire mensuel.", "Paie");
            return;
        }

        var maj = ClonerEmploye(EmployeEdition);
        maj.TauxSalaireBase = decimal.Round(
            maj.SalaireMensuel / RubriquesAPayerLt.JoursReference, 2, MidpointRounding.AwayFromZero);
        EmployeEdition = maj;
        StatutBarre = $"Taux salaire de base = {maj.TauxSalaireBase:N2} (mensuel ÷ 26)";
    }

    private static Employe ClonerEmploye(Employe s) => new()
    {
        Id = s.Id,
        Matricule = s.Matricule,
        Nom = s.Nom,
        Prenom = s.Prenom,
        CodePinZk = s.CodePinZk,
        Actif = s.Actif,
        SalaireMensuel = s.SalaireMensuel,
        TauxSalaireBase = s.TauxSalaireBase,
        TauxAnciennete = s.TauxAnciennete,
        TauxTransport = s.TauxTransport,
        TauxLogement = s.TauxLogement,
        TauxAllocFamiliales = s.TauxAllocFamiliales,
        TauxIndemniteKm = s.TauxIndemniteKm,
        TauxPrimeAssiduite = s.TauxPrimeAssiduite,
        TauxJourMaladie = s.TauxJourMaladie,
        TauxJourFerie = s.TauxJourFerie,
        TauxComplementTransport = s.TauxComplementTransport
    };

    private static void AppliquerFicheEmploye(Employe cible, Employe source)
    {
        cible.Matricule = source.Matricule.Trim();
        cible.Nom = source.Nom.Trim();
        cible.Prenom = source.Prenom?.Trim() ?? "";
        cible.CodePinZk = string.IsNullOrWhiteSpace(source.CodePinZk) ? null : source.CodePinZk.Trim();
        cible.Actif = source.Actif;
        cible.SalaireMensuel = source.SalaireMensuel;
        cible.TauxSalaireBase = source.TauxSalaireBase;
        cible.TauxAnciennete = source.TauxAnciennete;
        cible.TauxTransport = source.TauxTransport;
        cible.TauxLogement = source.TauxLogement;
        cible.TauxAllocFamiliales = source.TauxAllocFamiliales;
        cible.TauxIndemniteKm = source.TauxIndemniteKm;
        cible.TauxPrimeAssiduite = source.TauxPrimeAssiduite;
        cible.TauxJourMaladie = source.TauxJourMaladie;
        cible.TauxJourFerie = source.TauxJourFerie;
        cible.TauxComplementTransport = source.TauxComplementTransport;
    }

    private static void NormaliserMontantsPaie(Employe e)
    {
        static decimal Pos(decimal v) => v < 0 ? 0 : v;
        e.SalaireMensuel = Pos(e.SalaireMensuel);
        e.TauxSalaireBase = Pos(e.TauxSalaireBase);
        e.TauxAnciennete = Pos(e.TauxAnciennete);
        e.TauxTransport = Pos(e.TauxTransport);
        e.TauxLogement = Pos(e.TauxLogement);
        e.TauxAllocFamiliales = Pos(e.TauxAllocFamiliales);
        e.TauxIndemniteKm = Pos(e.TauxIndemniteKm);
        e.TauxPrimeAssiduite = Pos(e.TauxPrimeAssiduite);
        e.TauxJourMaladie = Pos(e.TauxJourMaladie);
        e.TauxJourFerie = Pos(e.TauxJourFerie);
        e.TauxComplementTransport = Pos(e.TauxComplementTransport);
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
        StatutBarre = type == PointageType.Entree
            ? $"✓ Entrée enregistrée à {now:HH:mm}"
            : $"✓ Sortie enregistrée à {now:HH:mm}";
    }

    private async Task SynchroniserZkAsync()
    {
        _syncEnCours = true;
        _syncErreur = "";
        ActualiserEtatSync();
        StatutBarre = "Synchronisation en cours…";
        var (ok, err, nb) = await ZktecoSynchronisationService.TrySynchroniserAsync();
        _syncEnCours = false;
        if (!ok)
        {
            _syncErreur = err ?? "Échec synchronisation";
            ActualiserEtatSync();
            WindowsNotificationService.NotifierSyncErreur(_syncErreur);
            MessageBox.Show(_syncErreur, "ZKTeco");
            StatutBarre = "Synchronisation échouée";
            return;
        }

        _syncErreur = "";
        ChargerPresenceJour();
        ChargerAccueil();
        ChargerParametres();
        ActualiserEtatSync();
        StatutBarre = $"✓ Synchronisation terminée — {nb} nouveau(x) pointage(s)";
        // Toast cinéma déjà émis via SynchroReussie
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
        _derniereSyncUtc = p.ZkDerniereSyncUtc;
        ZkDerniereSync = p.ZkDerniereSyncUtc.HasValue
            ? p.ZkDerniereSyncUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            : "Jamais";
        HeureDebutTravail = string.IsNullOrWhiteSpace(p.HeureDebutTravail)
            ? PresenceCalculService.FormatHhMm(PresenceCalculService.HeureDebutDefaut)
            : p.HeureDebutTravail;
        HeureLimiteTolerance = string.IsNullOrWhiteSpace(p.HeureLimiteTolerance)
            ? PresenceCalculService.FormatHhMm(PresenceCalculService.HeureLimiteDefaut)
            : p.HeureLimiteTolerance;
        HeureFinTravail = string.IsNullOrWhiteSpace(p.HeureFinTravail)
            ? PresenceCalculService.FormatHhMm(PresenceCalculService.HeureFinDefaut)
            : p.HeureFinTravail;
        NotificationsWindowsActives = p.NotificationsWindowsActives;
        DemarrerAvecWindows = p.DemarrerAvecWindows;
        if (LigneJourSelectionnee == null)
        {
            EditionEntree = HeureDebutTravail;
            EditionSortie = HeureFinTravail;
        }

        ActualiserEtatSync();
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
        p.NomEntreprise = string.IsNullOrWhiteSpace(NomEntreprise) ? "LT Services" : NomEntreprise.Trim();
        p.ZkTerminalIp = string.IsNullOrWhiteSpace(ZkIp) ? null : ZkIp.Trim();
        p.ZkTerminalPort = port;
        p.ZkMachineNumber = machine;
        p.ZkCommPassword = comm;
        p.ZkSyncActif = ZkSyncActif;
        p.ZkIntervalleSecondes = intervalle;
        p.NotificationsWindowsActives = NotificationsWindowsActives;
        p.DemarrerAvecWindows = DemarrerAvecWindows;
        p.HeureDebutTravail = PresenceCalculService.FormatHhMm(DebutTravailTs);
        p.HeureLimiteTolerance = PresenceCalculService.FormatHhMm(LimiteToleranceTs);
        p.HeureFinTravail = PresenceCalculService.FormatHhMm(FinTravailTs);
        HeureFinTravail = p.HeureFinTravail;
        HeureDebutTravail = p.HeureDebutTravail;
        HeureLimiteTolerance = p.HeureLimiteTolerance;
        if (LigneJourSelectionnee == null)
        {
            EditionEntree = HeureDebutTravail;
            EditionSortie = HeureFinTravail;
        }

        db.SaveChanges();
        DemarrageWindowsService.Appliquer(DemarrerAvecWindows);
        ZktecoSynchronisationService.Reconfigurer();
        ChargerPresenceJour();
        ChargerDetailSelection();
        NotifierDetailUi();
        StatutBarre = "Paramètres enregistrés";
        MessageBox.Show("Paramètres enregistrés.", "LT Services");
    }

    private async Task VerifierMiseAJourAsync()
    {
        if (MiseAJourEnCours)
            return;

        MiseAJourEnCours = true;
        MessageMiseAJour = "Vérification en cours…";
        StatutBarre = "Vérification des mises à jour…";
        try
        {
            var result = await ApplicationUpdateService.VerifierAsync().ConfigureAwait(true);
            MessageMiseAJour = result.Message;

            if (result.Kind == UpdateCheckResultKind.UpToDate)
            {
                StatutBarre = "✓ " + result.Message;
                MessageBox.Show(result.Message, "Mise à jour — LT Présence",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (result.Kind == UpdateCheckResultKind.Error || result.Manifest == null)
            {
                StatutBarre = result.Message;
                MessageBox.Show(result.Message, "Mise à jour — LT Présence",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var notes = string.IsNullOrWhiteSpace(result.Manifest.ReleaseNotes)
                ? ""
                : "\n\n" + result.Manifest.ReleaseNotes.Trim();
            var ok = MessageBox.Show(
                result.Message + notes + "\n\nTélécharger et installer maintenant ?",
                "Mise à jour disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes)
            {
                StatutBarre = "Mise à jour reportée";
                return;
            }

            MessageMiseAJour = "Téléchargement…";
            StatutBarre = "Téléchargement de la mise à jour…";
            var progress = new Progress<double>(p =>
                MessageMiseAJour = $"Téléchargement… {p:0}%");
            var dl = await ApplicationUpdateService
                .TelechargerAsync(result.Manifest, progress)
                .ConfigureAwait(true);
            if (!dl.Success || string.IsNullOrWhiteSpace(dl.CheminInstallateur))
            {
                MessageMiseAJour = dl.Message;
                StatutBarre = dl.Message;
                MessageBox.Show(dl.Message, "Mise à jour — LT Présence",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ApplicationUpdateService.LancerInstallateur(dl.CheminInstallateur, out var msg))
            {
                MessageMiseAJour = msg;
                MessageBox.Show(msg, "Mise à jour — LT Présence",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageMiseAJour = "Installateur lancé — suivez l’assistant.";
            StatutBarre = "✓ Installateur de mise à jour lancé";
            MessageBox.Show(
                "L’installateur va s’ouvrir. Fermez LT Présence si l’assistant le demande, puis terminez l’installation.",
                "Mise à jour — LT Présence",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            MiseAJourEnCours = false;
        }
    }

    private string NomEntrepriseCourant()
    {
        using var db = new PresenceDbContext();
        return db.Parametres.AsNoTracking().FirstOrDefault()?.NomEntreprise ?? "LT Services";
    }

    private void ExporterExcelJour()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Presence_{DatePresence:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        PresenceExportService.ExporterExcelDetailJour(dlg.FileName, NomEntrepriseCourant(), DatePresence, _presenceJourBrute);
        StatutBarre = "✓ Export Excel terminé";
    }

    private void ExporterCsvJour()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"Presence_{DatePresence:yyyyMMdd}.csv"
        };
        if (dlg.ShowDialog() != true) return;
        PresenceExportService.ExporterCsvDetailJour(dlg.FileName, NomEntrepriseCourant(), DatePresence, _presenceJourBrute);
        StatutBarre = "✓ Export CSV terminé";
    }

    private void ExporterPdfJour()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Presence_{DatePresence:yyyyMMdd}.pdf"
        };
        if (dlg.ShowDialog() != true) return;
        PresenceExportService.ExporterPdfDetailJour(dlg.FileName, NomEntrepriseCourant(), DatePresence, _presenceJourBrute);
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
            r.Absences,
            r.Retards)).ToList();
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
            r.Absences,
            r.Retards)).ToList();
        PresenceExportService.ExporterPdfResumeMois(dlg.FileName, NomEntrepriseCourant(), AnneeRapport, MoisRapport, data);
        StatutBarre = "Export PDF mois OK";
    }
}

