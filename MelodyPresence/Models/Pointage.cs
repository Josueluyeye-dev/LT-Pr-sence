namespace MelodyPresence.Models;

public enum PointageSource
{
    Manuel = 0,
    Terminal = 1
}

public enum PointageType
{
    Inconnu = 0,
    Entree = 1,
    Sortie = 2
}

public class Pointage
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public DateTime Horodatage { get; set; }
    public PointageSource Source { get; set; }
    public PointageType Type { get; set; }

    public string TypeLibelle => Type switch
    {
        PointageType.Entree => "Entrée",
        PointageType.Sortie => "Sortie",
        _ => "—"
    };

    public string SourceLibelle => Source == PointageSource.Terminal ? "Terminal" : "Manuel";
}
