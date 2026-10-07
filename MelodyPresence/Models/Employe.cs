namespace MelodyPresence.Models;

public class Employe
{
    public int Id { get; set; }
    public string Matricule { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string? CodePinZk { get; set; }
    public bool Actif { get; set; } = true;

    /// <summary>Salaire mensuel de base (devise entreprise, ex. USD / CDF).</summary>
    public decimal SalaireMensuel { get; set; }

    /// <summary>Taux journaliers section « A PAYER » (bulletin LT).</summary>
    public decimal TauxSalaireBase { get; set; }
    public decimal TauxAnciennete { get; set; }
    public decimal TauxTransport { get; set; }
    public decimal TauxLogement { get; set; }
    public decimal TauxAllocFamiliales { get; set; }
    public decimal TauxIndemniteKm { get; set; }
    public decimal TauxPrimeAssiduite { get; set; }
    public decimal TauxJourMaladie { get; set; }
    public decimal TauxJourFerie { get; set; }
    public decimal TauxComplementTransport { get; set; }

    public string NomComplet => string.IsNullOrWhiteSpace(Prenom) ? Nom : $"{Nom} {Prenom}".Trim();

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
}
