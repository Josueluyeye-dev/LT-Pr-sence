namespace MelodyPresence.Models;

public class Employe
{
    public int Id { get; set; }
    public string Matricule { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string? CodePinZk { get; set; }
    public bool Actif { get; set; } = true;

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
