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
    public bool EstEnRetard { get; set; }
    public int MinutesRetard { get; set; }
    public int NbPointages { get; set; }

    public string EntreeAffichee => PremiereEntree?.ToString("HH:mm") ?? "—";
    public string SortieAffichee => DerniereSortie?.ToString("HH:mm") ?? "—";
    public string HeuresAffichees => NbPointages == 0 ? "—" : FormatHeures(Heures);
    public string RetardAffiche => EstEnRetard && MinutesRetard > 0 ? $"+{MinutesRetard} min" : "—";

    public string StatutLibelle => Statut switch
    {
        "Présent" => "Présent",
        "Parti" => "Parti",
        "En cours" => "En cours",
        "Retard" => "Retard",
        "Non pointé" => "Non pointé",
        _ => "Absent"
    };

    public string StatutIcone => Statut switch
    {
        "Présent" or "Parti" => "●",
        "En cours" => "●",
        "Retard" => "●",
        "Non pointé" => "○",
        _ => "●"
    };

    /// <summary>Entrée possible s'il n'y a aucun pointage, ou si le cycle précédent est fermé (sortie enregistrée).</summary>
    public bool PeutPointerEntree => NbPointages == 0 || DerniereSortie.HasValue;

    /// <summary>Sortie possible dès qu'il y a une entrée et pas encore de sortie.</summary>
    public bool PeutPointerSortie => PremiereEntree.HasValue && !DerniereSortie.HasValue;

    public string Initiales
    {
        get
        {
            var parts = NomComplet.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }
    }

    public static string FormatHeures(double heures)
    {
        if (heures <= 0) return "00:00";
        var ts = TimeSpan.FromHours(heures);
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}";
    }
}
