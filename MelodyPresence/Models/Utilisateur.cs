namespace MelodyPresence.Models;

public class Utilisateur
{
    public int Id { get; set; }
    public string Identifiant { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public string MotDePasseHash { get; set; } = "";
    public string MotDePasseSel { get; set; } = "";
    public string Role { get; set; } = "Administrateur";
    public DateTime DateCreation { get; set; } = DateTime.Now;
    public DateTime? DerniereConnexion { get; set; }
    public bool Actif { get; set; } = true;
}
