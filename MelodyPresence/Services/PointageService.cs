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

    /// <summary>
    /// Fusion des logs terminal — même logique que Melody Paie :
    /// priorité ID terminal (<see cref="Employe.CodePinZk"/>), puis matricule (clés + chiffres sans zéros).
    /// </summary>
    public int FusionnerDepuisTerminal(IReadOnlyList<(string CodePin, DateTime Horodatage)> logs)
    {
        if (logs.Count == 0) return 0;

        using var db = new PresenceDbContext();
        var employes = db.Employes.AsNoTracking().Where(e => e.Actif).ToList();
        var pinMap = ConstruireMapEmployes(employes);

        var ajoutes = 0;
        foreach (var (codePin, horodatage) in logs)
        {
            if (!ResoudreEmployeId(pinMap, codePin, out var employeId))
                continue;

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

    public static Dictionary<string, int> ConstruireMapEmployes(IEnumerable<Employe> employes)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in employes)
        {
            AjouterCles(map, e.CodePinZk, e.Id);
            AjouterCles(map, e.Matricule, e.Id);
        }

        return map;
    }

    public static bool ResoudreEmployeId(Dictionary<string, int> map, string codePin, out int employeId)
    {
        employeId = 0;
        var cle = NormaliserCle(codePin);
        if (!string.IsNullOrWhiteSpace(cle) && map.TryGetValue(cle, out employeId))
            return true;

        var digits = NormaliserChiffres(codePin);
        if (!string.IsNullOrWhiteSpace(digits) && map.TryGetValue(digits, out employeId))
            return true;

        return false;
    }

    private static void AjouterCles(Dictionary<string, int> map, string? valeur, int employeId)
    {
        var brut = (valeur ?? "").Trim();
        if (brut.Length == 0) return;

        var cle = NormaliserCle(brut);
        if (!string.IsNullOrWhiteSpace(cle) && !map.ContainsKey(cle))
            map.Add(cle, employeId);

        var digits = NormaliserChiffres(brut);
        if (!string.IsNullOrWhiteSpace(digits) && !map.ContainsKey(digits))
            map.Add(digits, employeId);
    }

    private static string NormaliserCle(string valeur)
        => (valeur ?? "").Trim().Replace(" ", "").ToUpperInvariant();

    private static string NormaliserChiffres(string valeur)
    {
        var digits = new string((valeur ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return "";
        var sansZeros = digits.TrimStart('0');
        return string.IsNullOrEmpty(sansZeros) ? "0" : sansZeros;
    }

    private static PointageType DeterminerType(PresenceDbContext db, int employeId, DateTime local)
    {
        var jour = local.Date;
        var lendemain = jour.AddDays(1);
        var count = db.Pointages.Count(p => p.EmployeId == employeId && p.Horodatage >= jour && p.Horodatage < lendemain);
        return count % 2 == 0 ? PointageType.Entree : PointageType.Sortie;
    }
}
