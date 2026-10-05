namespace MelodyPresence.Models;

public class ParametresApplication
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string NomEntreprise { get; set; } = "Mon entreprise";
    public string? ZkTerminalIp { get; set; }
    public int ZkTerminalPort { get; set; } = 4370;
    public int ZkMachineNumber { get; set; } = 1;
    public int ZkCommPassword { get; set; }
    public bool ZkSyncActif { get; set; }
    public int ZkIntervalleSecondes { get; set; } = 60;
    public DateTime? ZkDerniereSyncUtc { get; set; }
}
