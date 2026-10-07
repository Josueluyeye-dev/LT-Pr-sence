namespace MelodyPresence.Models;

public class ParametresApplication
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string NomEntreprise { get; set; } = "LT Services";
    public string? ZkTerminalIp { get; set; }
    public int ZkTerminalPort { get; set; } = 4370;
    public int ZkMachineNumber { get; set; } = 1;
    public int ZkCommPassword { get; set; }
    /// <summary>Sync auto du terminal (comme Melody Paie) — active dès qu'une IP est configurée.</summary>
    public bool ZkSyncActif { get; set; } = true;
    public int ZkIntervalleSecondes { get; set; } = 60;
    public DateTime? ZkDerniereSyncUtc { get; set; }

    /// <summary>Heure d'arrivée attendue (ex. 07:30).</summary>
    public string HeureDebutTravail { get; set; } = "07:30";

    /// <summary>Après cette heure = retard (ex. 07:40).</summary>
    public string HeureLimiteTolerance { get; set; } = "07:40";

    /// <summary>Fin de journée attendue (ex. 17:00).</summary>
    public string HeureFinTravail { get; set; } = "17:00";

    /// <summary>Toasts Windows + son pour les sync ZK.</summary>
    public bool NotificationsWindowsActives { get; set; } = true;

    /// <summary>Lancer l'application au démarrage de Windows.</summary>
    public bool DemarrerAvecWindows { get; set; } = true;

    /// <summary>Chemin vers MelodyPaieRDC.exe (modules Calcul, Bulletins, Déclarations…).</summary>
    public string? CheminMelodyPaie { get; set; }
}
