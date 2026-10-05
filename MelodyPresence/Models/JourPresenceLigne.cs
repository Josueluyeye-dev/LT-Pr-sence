namespace MelodyPresence.Models;

public class JourPresenceLigne
{
    public int EmployeId { get; set; }
    public string Matricule { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime? PremiereEntree { get; set; }
    public DateTime? DerniereSortie { get; set; }
    public double Heures { get; set; }
    public string Statut { get; set; } = "Absent";
    public int NbPointages { get; set; }
}
