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
}
