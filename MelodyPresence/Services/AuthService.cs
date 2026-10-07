using System.Security.Cryptography;
using System.Text;
using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public static class AuthService
{
    public static Utilisateur? UtilisateurCourant { get; private set; }

    public static bool EstConnecte => UtilisateurCourant != null;

    public static bool ADesComptes()
    {
        using var db = new PresenceDbContext();
        return db.Utilisateurs.Any();
    }

    public static (bool Ok, string Message) CreerCompte(
        string identifiant,
        string nomComplet,
        string motDePasse,
        string confirmation)
    {
        identifiant = (identifiant ?? "").Trim();
        nomComplet = (nomComplet ?? "").Trim();

        if (identifiant.Length < 3)
            return (false, "L’identifiant doit contenir au moins 3 caractères.");
        if (string.IsNullOrWhiteSpace(nomComplet))
            return (false, "Indiquez votre nom complet.");
        if (string.IsNullOrEmpty(motDePasse) || motDePasse.Length < 6)
            return (false, "Le mot de passe doit contenir au moins 6 caractères.");
        if (!string.Equals(motDePasse, confirmation, StringComparison.Ordinal))
            return (false, "La confirmation du mot de passe ne correspond pas.");

        using var db = new PresenceDbContext();
        if (db.Utilisateurs.Any(u => u.Identifiant.ToLower() == identifiant.ToLower()))
            return (false, "Cet identifiant existe déjà.");

        var (hash, sel) = Hasher(motDePasse);
        var user = new Utilisateur
        {
            Identifiant = identifiant,
            NomComplet = nomComplet,
            MotDePasseHash = hash,
            MotDePasseSel = sel,
            Role = db.Utilisateurs.Any() ? "Utilisateur" : "Administrateur",
            DateCreation = DateTime.Now,
            Actif = true
        };
        db.Utilisateurs.Add(user);
        db.SaveChanges();
        return (true, "Compte créé. Vous pouvez vous connecter.");
    }

    public static (bool Ok, string Message, Utilisateur? User) Connexion(string identifiant, string motDePasse)
    {
        identifiant = (identifiant ?? "").Trim();
        if (string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrEmpty(motDePasse))
            return (false, "Saisissez l’identifiant et le mot de passe.", null);

        using var db = new PresenceDbContext();
        var user = db.Utilisateurs.FirstOrDefault(u =>
            u.Actif && u.Identifiant.ToLower() == identifiant.ToLower());
        if (user == null)
            return (false, "Identifiant ou mot de passe incorrect.", null);

        if (!Verifier(motDePasse, user.MotDePasseHash, user.MotDePasseSel))
            return (false, "Identifiant ou mot de passe incorrect.", null);

        user.DerniereConnexion = DateTime.Now;
        db.SaveChanges();
        UtilisateurCourant = user;
        return (true, "Connexion réussie.", user);
    }

    public static void Deconnecter() => UtilisateurCourant = null;

    private static (string Hash, string Sel) Hasher(string motDePasse)
    {
        var selBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(motDePasse),
            selBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(selBytes));
    }

    private static bool Verifier(string motDePasse, string hashB64, string selB64)
    {
        try
        {
            var sel = Convert.FromBase64String(selB64);
            var attendu = Convert.FromBase64String(hashB64);
            var calcule = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(motDePasse),
                sel,
                100_000,
                HashAlgorithmName.SHA256,
                32);
            return CryptographicOperations.FixedTimeEquals(calcule, attendu);
        }
        catch
        {
            return false;
        }
    }
}
