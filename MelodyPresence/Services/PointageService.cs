using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public class PointageService
{
    public Pointage EnregistrerManuel(int employeId, DateTime horodatage, PointageType type)
    {
        using var db = new PresenceDbContext();
        var p = new Pointage
        {
            EmployeId = employeId,
            Horodatage = DateTime.SpecifyKind(horodatage, DateTimeKind.Local),
            Source = PointageSource.Manuel,
            Type = type
        };
        db.Pointages.Add(p);
        db.SaveChanges();
        return p;
    }

    public IReadOnlyList<Pointage> ListerDuJour(int employeId, DateTime date)
    {
        using var db = new PresenceDbContext();
        var debut = date.Date;
        var fin = debut.AddDays(1);
        return db.Pointages.AsNoTracking()
            .Where(p => p.EmployeId == employeId && p.Horodatage >= debut && p.Horodatage < fin)
            .OrderBy(p => p.Horodatage)
            .ToList();
    }

    public bool Supprimer(int pointageId)
    {
        using var db = new PresenceDbContext();
        var p = db.Pointages.FirstOrDefault(x => x.Id == pointageId);
        if (p == null) return false;
        db.Pointages.Remove(p);
        db.SaveChanges();
        return true;
    }

    public int SupprimerTousDuJour(int employeId, DateTime date)
    {
        using var db = new PresenceDbContext();
        var debut = date.Date;
        var fin = debut.AddDays(1);
        var liste = db.Pointages
            .Where(p => p.EmployeId == employeId && p.Horodatage >= debut && p.Horodatage < fin)
            .ToList();
        if (liste.Count == 0) return 0;
        db.Pointages.RemoveRange(liste);
        db.SaveChanges();
        return liste.Count;
    }

    public bool SupprimerDernierDuJour(int employeId, DateTime date)
    {
        using var db = new PresenceDbContext();
        var debut = date.Date;
        var fin = debut.AddDays(1);
        var dernier = db.Pointages
            .Where(p => p.EmployeId == employeId && p.Horodatage >= debut && p.Horodatage < fin)
            .OrderByDescending(p => p.Horodatage)
            .FirstOrDefault();
        if (dernier == null) return false;
        db.Pointages.Remove(dernier);
        db.SaveChanges();
        return true;
    }

    public bool ModifierHorodatage(int pointageId, DateTime nouvelHorodatage)
    {
        using var db = new PresenceDbContext();
        var p = db.Pointages.FirstOrDefault(x => x.Id == pointageId);
        if (p == null) return false;
        p.Horodatage = DateTime.SpecifyKind(nouvelHorodatage, DateTimeKind.Local);
        db.SaveChanges();
        return true;
    }

    /// <summary>Remplace la journée par une entrée et/ou une sortie manuelles.</summary>
    public void DefinirJournee(int employeId, DateTime date, TimeSpan? entree, TimeSpan? sortie)
    {
        using var db = new PresenceDbContext();
        var debut = date.Date;
        var fin = debut.AddDays(1);
        var existants = db.Pointages
            .Where(p => p.EmployeId == employeId && p.Horodatage >= debut && p.Horodatage < fin)
            .ToList();
        if (existants.Count > 0)
            db.Pointages.RemoveRange(existants);

        if (entree.HasValue)
        {
            db.Pointages.Add(new Pointage
            {
                EmployeId = employeId,
                Horodatage = debut.Add(entree.Value),
                Source = PointageSource.Manuel,
                Type = PointageType.Entree
            });
        }

        if (sortie.HasValue)
        {
            db.Pointages.Add(new Pointage
            {
                EmployeId = employeId,
                Horodatage = debut.Add(sortie.Value),
                Source = PointageSource.Manuel,
                Type = PointageType.Sortie
            });
        }

        db.SaveChanges();
    }

    public int FusionnerDepuisTerminal(IReadOnlyList<(string CodePin, DateTime Horodatage)> logs)
    {
        if (logs.Count == 0) return 0;

        using var db = new PresenceDbContext();
        var employes = db.Employes.AsNoTracking().Where(e => e.Actif && e.CodePinZk != null && e.CodePinZk != "").ToList();
        var pinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in employes)
        {
            var pin = (e.CodePinZk ?? "").Trim();
            if (pin.Length == 0) continue;
            pinMap[pin] = e.Id;
            var digits = new string(pin.Where(char.IsDigit).ToArray());
            if (digits.Length > 0)
                pinMap[digits] = e.Id;
        }

        var ajoutes = 0;
        foreach (var (codePin, horodatage) in logs)
        {
            var cle = codePin.Trim();
            if (!pinMap.TryGetValue(cle, out var employeId))
            {
                var digits = new string(cle.Where(char.IsDigit).ToArray());
                if (digits.Length == 0 || !pinMap.TryGetValue(digits, out employeId))
                    continue;
            }

            var local = horodatage.Kind == DateTimeKind.Utc
                ? horodatage.ToLocalTime()
                : DateTime.SpecifyKind(horodatage, DateTimeKind.Local);

            var debutMin = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0);
            var finMin = debutMin.AddMinutes(1);
            var existe = db.Pointages.Any(p =>
                p.EmployeId == employeId &&
                p.Source == PointageSource.Terminal &&
                p.Horodatage >= debutMin && p.Horodatage < finMin);
            if (existe) continue;

            var type = DeterminerType(db, employeId, local);
            db.Pointages.Add(new Pointage
            {
                EmployeId = employeId,
                Horodatage = local,
                Source = PointageSource.Terminal,
                Type = type
            });
            ajoutes++;
        }

        if (ajoutes > 0)
            db.SaveChanges();
        return ajoutes;
    }

    private static PointageType DeterminerType(PresenceDbContext db, int employeId, DateTime local)
    {
        var jour = local.Date;
        var lendemain = jour.AddDays(1);
        var count = db.Pointages.Count(p => p.EmployeId == employeId && p.Horodatage >= jour && p.Horodatage < lendemain);
        return count % 2 == 0 ? PointageType.Entree : PointageType.Sortie;
    }
}
