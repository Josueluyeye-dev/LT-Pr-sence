namespace MelodyPresence.Models;

public class ResumeMoisItem
{
    public string Matricule { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public int JoursPresents { get; set; }
    public double HeuresTotales { get; set; }
    public int Absences { get; set; }
    public int Retards { get; set; }
}
