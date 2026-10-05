using MelodyPresence.Models;

namespace MelodyPresence.Services;

public static class PresenceCalculService
{
    public static JourPresenceLigne CalculerJour(Employe employe, IEnumerable<Pointage> pointagesDuJour, DateTime date)
    {
        var liste = pointagesDuJour
            .Where(p => p.EmployeId == employe.Id && p.Horodatage.Date == date.Date)
            .OrderBy(p => p.Horodatage)
            .ToList();

        DateTime? premiere = liste.FirstOrDefault()?.Horodatage;
        DateTime? derniere = liste.Count > 1 ? liste.Last().Horodatage : null;
        double heures = 0;
        if (premiere.HasValue && derniere.HasValue && derniere > premiere)
            heures = Math.Round((derniere.Value - premiere.Value).TotalHours, 2);

        var statut = liste.Count == 0 ? "Absent" : (derniere.HasValue ? "Présent" : "En cours");

        return new JourPresenceLigne
        {
            EmployeId = employe.Id,
            Matricule = employe.Matricule,
            NomComplet = employe.NomComplet,
            Date = date.Date,
            PremiereEntree = premiere,
            DerniereSortie = derniere,
            Heures = heures,
            Statut = statut,
            NbPointages = liste.Count
        };
    }

    public static IReadOnlyList<JourPresenceLigne> CalculerJourPourTous(
        IEnumerable<Employe> employes,
        IEnumerable<Pointage> pointages,
        DateTime date)
    {
        var actifs = employes.Where(e => e.Actif).OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();
        var pts = pointages.Where(p => p.Horodatage.Date == date.Date).ToList();
        return actifs.Select(e => CalculerJour(e, pts, date)).ToList();
    }

    public static IReadOnlyList<JourPresenceLigne> CalculerMois(
        IEnumerable<Employe> employes,
        IEnumerable<Pointage> pointages,
        int annee,
        int mois)
    {
        var debut = new DateTime(annee, mois, 1);
        var fin = debut.AddMonths(1);
        var actifs = employes.Where(e => e.Actif).ToList();
        var pts = pointages.Where(p => p.Horodatage >= debut && p.Horodatage < fin).ToList();
        var result = new List<JourPresenceLigne>();

        for (var d = debut; d < fin; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            result.AddRange(CalculerJourPourTous(actifs, pts, d));
        }

        return result;
    }

    public static IReadOnlyList<(Employe Employe, int JoursPresents, double HeuresTotales, int Absences)> ResumeMensuel(
        IEnumerable<Employe> employes,
        IEnumerable<Pointage> pointages,
        int annee,
        int mois)
    {
        var lignes = CalculerMois(employes, pointages, annee, mois);
        return lignes
            .GroupBy(l => l.EmployeId)
            .Select(g =>
            {
                var first = g.First();
                var emp = new Employe
                {
                    Id = first.EmployeId,
                    Matricule = first.Matricule,
                    Nom = first.NomComplet
                };
                return (
                    emp,
                    g.Count(x => x.Statut is "Présent" or "En cours"),
                    Math.Round(g.Sum(x => x.Heures), 2),
                    g.Count(x => x.Statut == "Absent")
                );
            })
            .OrderBy(x => x.emp.Nom)
            .ToList();
    }
}
