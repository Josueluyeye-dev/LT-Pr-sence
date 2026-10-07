using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public static class RetenuePaieService
{
    public static IReadOnlyList<RetenuePaie> ListerMois(int annee, int mois, int? employeId = null)
    {
        using var db = new PresenceDbContext();
        var q = db.Retenues.AsNoTracking()
            .Include(r => r.Employe)
            .Where(r => r.Annee == annee && r.Mois == mois);
        if (employeId is > 0)
            q = q.Where(r => r.EmployeId == employeId.Value);
        return q.OrderBy(r => r.Employe!.Nom).ThenBy(r => r.Type).ThenBy(r => r.Id).ToList();
    }

    public static decimal TotalPourEmploye(int employeId, int annee, int mois)
    {
        using var db = new PresenceDbContext();
        return db.Retenues
            .Where(r => r.EmployeId == employeId && r.Annee == annee && r.Mois == mois)
            .Sum(r => (decimal?)r.Montant) ?? 0m;
    }

    public static List<LigneBulletinRetenue> LignesPourBulletin(int employeId, int annee, int mois)
    {
        using var db = new PresenceDbContext();
        return db.Retenues.AsNoTracking()
            .Where(r => r.EmployeId == employeId && r.Annee == annee && r.Mois == mois && r.Montant > 0)
            .OrderBy(r => r.Type).ThenBy(r => r.Id)
            .Select(r => new LigneBulletinRetenue
            {
                Type = r.Type,
                Libelle = string.IsNullOrWhiteSpace(r.LibelleLibre)
                    ? TypesRetenue.Libelle(r.Type)
                    : r.LibelleLibre!,
                Montant = r.Montant
            })
            .ToList();
    }

    public static RetenuePaie Enregistrer(RetenuePaie saisie)
    {
        if (saisie.EmployeId <= 0)
            throw new InvalidOperationException("Sélectionnez un employé.");
        if (string.IsNullOrWhiteSpace(saisie.Type))
            throw new InvalidOperationException("Type de retenue obligatoire.");
        if (saisie.Montant < 0)
            throw new InvalidOperationException("Le montant ne peut pas être négatif.");
        if (saisie.Mois is < 1 or > 12)
            throw new InvalidOperationException("Mois invalide.");

        using var db = new PresenceDbContext();
        if (!db.Employes.Any(e => e.Id == saisie.EmployeId))
            throw new InvalidOperationException("Employé introuvable.");

        RetenuePaie entity;
        if (saisie.Id > 0)
        {
            entity = db.Retenues.FirstOrDefault(r => r.Id == saisie.Id)
                     ?? throw new InvalidOperationException("Retenue introuvable.");
            entity.EmployeId = saisie.EmployeId;
            entity.Annee = saisie.Annee;
            entity.Mois = saisie.Mois;
            entity.Type = saisie.Type.Trim();
            entity.LibelleLibre = string.IsNullOrWhiteSpace(saisie.LibelleLibre) ? null : saisie.LibelleLibre.Trim();
            entity.Montant = decimal.Round(saisie.Montant, 2, MidpointRounding.AwayFromZero);
            entity.Notes = string.IsNullOrWhiteSpace(saisie.Notes) ? null : saisie.Notes.Trim();
        }
        else
        {
            entity = new RetenuePaie
            {
                EmployeId = saisie.EmployeId,
                Annee = saisie.Annee,
                Mois = saisie.Mois,
                Type = saisie.Type.Trim(),
                LibelleLibre = string.IsNullOrWhiteSpace(saisie.LibelleLibre) ? null : saisie.LibelleLibre.Trim(),
                Montant = decimal.Round(saisie.Montant, 2, MidpointRounding.AwayFromZero),
                Notes = string.IsNullOrWhiteSpace(saisie.Notes) ? null : saisie.Notes.Trim(),
                DateSaisie = DateTime.Now
            };
            db.Retenues.Add(entity);
        }

        db.SaveChanges();
        return entity;
    }

    public static bool Supprimer(int id)
    {
        using var db = new PresenceDbContext();
        var r = db.Retenues.FirstOrDefault(x => x.Id == id);
        if (r == null) return false;
        db.Retenues.Remove(r);
        db.SaveChanges();
        return true;
    }

}
