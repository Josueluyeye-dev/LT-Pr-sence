using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPresence.Models;

/// <summary>Types de retenues LT (hors retenue retards automatique).</summary>
public static class TypesRetenue
{
    public const string RetenueTransport = "RetenueTransport";
    public const string PointageHoraire = "PointageHoraire";
    public const string AvanceQuinzaine = "AvanceQuinzaine";
    public const string PretSociete = "PretSociete";
    public const string PretScolaire = "PretScolaire";
    public const string AutrePret = "AutrePret";
    public const string Cnss = "CNSS";
    public const string Ipr = "IPR";
    public const string AutresRetenues = "AutresRetenues";

    public static IReadOnlyList<TypeRetenueItem> Tous { get; } =
    [
        new(RetenueTransport, "Retenue transport"),
        new(PointageHoraire, "Pointage horaire"),
        new(AvanceQuinzaine, "Avance quinzaine"),
        new(PretSociete, "Prêt société"),
        new(PretScolaire, "Prêt scolaire"),
        new(AutrePret, "Autre prêt"),
        new(Cnss, "CNSS"),
        new(Ipr, "IPR"),
        new(AutresRetenues, "Autres retenues")
    ];

    public static string Libelle(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Retenue";
        var t = Tous.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        return t?.Libelle ?? code;
    }
}

public sealed class TypeRetenueItem
{
    public TypeRetenueItem(string code, string libelle)
    {
        Code = code;
        Libelle = libelle;
    }

    public string Code { get; }
    public string Libelle { get; }
    public override string ToString() => Libelle;
}

/// <summary>Retenue saisie pour un employé sur une période de paie (mois de paiement).</summary>
public class RetenuePaie
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public int Annee { get; set; }
    public int Mois { get; set; }
    public string Type { get; set; } = TypesRetenue.AutresRetenues;
    public string? LibelleLibre { get; set; }
    public decimal Montant { get; set; }
    public string? Notes { get; set; }
    public DateTime DateSaisie { get; set; } = DateTime.Now;

    [NotMapped]
    public string TypeLibelle =>
        string.IsNullOrWhiteSpace(LibelleLibre) ? TypesRetenue.Libelle(Type) : LibelleLibre.Trim();

    [NotMapped]
    public string EmployeLibelle => Employe?.NomComplet ?? $"#{EmployeId}";
}

/// <summary>Ligne de retenue figée sur un bulletin (JSON).</summary>
public sealed class LigneBulletinRetenue
{
    public string Type { get; set; } = "";
    public string Libelle { get; set; } = "";
    public decimal Montant { get; set; }
}
