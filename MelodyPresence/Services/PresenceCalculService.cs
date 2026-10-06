using System.Globalization;
using MelodyPresence.Models;

namespace MelodyPresence.Services;

public static class PresenceCalculService
{
    public static readonly TimeSpan HeureDebutDefaut = new(7, 30, 0);
    public static readonly TimeSpan HeureLimiteDefaut = new(7, 40, 0);
    public static readonly TimeSpan HeureFinDefaut = new(17, 0, 0);

    public static string FormatHhMm(TimeSpan t) => t.ToString(@"hh\:mm");

    public static JourPresenceLigne CalculerJour(
        Employe employe,
        IEnumerable<Pointage> pointagesDuJour,
        DateTime date,
        TimeSpan? heureDebut = null,
        TimeSpan? heureLimite = null)
    {
        var debut = heureDebut ?? HeureDebutDefaut;
        var limite = heureLimite ?? HeureLimiteDefaut;
        if (limite < debut)
            limite = debut;

        var liste = pointagesDuJour
            .Where(p => p.EmployeId == employe.Id && p.Horodatage.Date == date.Date)
            .OrderBy(p => p.Horodatage)
            .ToList();

        DateTime? premiere = liste.FirstOrDefault()?.Horodatage;
        DateTime? derniere = liste.Count > 1 ? liste.Last().Horodatage : null;
        double heures = 0;
        if (premiere.HasValue && derniere.HasValue && derniere > premiere)
            heures = Math.Round((derniere.Value - premiere.Value).TotalHours, 2);

        var estRetard = false;
        var minutesRetard = 0;
        if (premiere.HasValue)
        {
            var heureArrivee = premiere.Value.TimeOfDay;
            if (heureArrivee > limite)
            {
                estRetard = true;
                minutesRetard = (int)Math.Ceiling((heureArrivee - debut).TotalMinutes);
            }
        }

        string statut;
        if (liste.Count == 0)
        {
            // Avant la tolérance du jour courant : "Non pointé" (pas encore Absent).
            if (date.Date == DateTime.Today && DateTime.Now.TimeOfDay < limite)
                statut = "Non pointé";
            else
                statut = "Absent";
        }
        else if (estRetard)
            statut = "Retard";
        else if (derniere.HasValue)
            statut = "Parti";
        else
            statut = "En cours";

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
            EstEnRetard = estRetard,
            MinutesRetard = minutesRetard,
            NbPointages = liste.Count
        };
    }

    public static IReadOnlyList<JourPresenceLigne> CalculerJourPourTous(
        IEnumerable<Employe> employes,
        IEnumerable<Pointage> pointages,
        DateTime date,
        TimeSpan? heureDebut = null,
        TimeSpan? heureLimite = null)
    {
        var actifs = employes.Where(e => e.Actif).OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToList();
        var pts = pointages.Where(p => p.Horodatage.Date == date.Date).ToList();
        return actifs.Select(e => CalculerJour(e, pts, date, heureDebut, heureLimite)).ToList();
    }

    public static IReadOnlyList<JourPresenceLigne> CalculerMois(
        IEnumerable<Employe> employes,
        IEnumerable<Pointage> pointages,
        int annee,
        int mois,
        TimeSpan? heureDebut = null,
        TimeSpan? heureLimite = null)
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
            result.AddRange(CalculerJourPourTous(actifs, pts, d, heureDebut, heureLimite));
        }

        return result;
    }

    public static IReadOnlyList<(Employe Employe, int JoursPresents, double HeuresTotales, int Absences, int Retards)> ResumeMensuel(
        IEnumerable<Employe> employes,
        IEnumerable<Pointage> pointages,
        int annee,
        int mois,
        TimeSpan? heureDebut = null,
        TimeSpan? heureLimite = null)
    {
        var lignes = CalculerMois(employes, pointages, annee, mois, heureDebut, heureLimite);
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
                    g.Count(x => x.Statut is "Présent" or "Parti" or "En cours" or "Retard"),
                    Math.Round(g.Sum(x => x.Heures), 2),
                    g.Count(x => x.Statut is "Absent" or "Non pointé"),
                    g.Count(x => x.EstEnRetard)
                );
            })
            .OrderBy(x => x.emp.Nom)
            .ToList();
    }

    public static TimeSpan ParserHeure(string? texte, TimeSpan defaut)
    {
        if (string.IsNullOrWhiteSpace(texte))
            return defaut;
        if (TimeSpan.TryParseExact(texte.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var t))
            return t;
        if (TimeSpan.TryParseExact(texte.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out t))
            return t;
        if (TimeSpan.TryParse(texte.Trim(), CultureInfo.InvariantCulture, out t))
            return t;
        return defaut;
    }

    public static (TimeSpan Debut, TimeSpan Limite) LireHoraires(ParametresApplication? p)
    {
        var debut = ParserHeure(p?.HeureDebutTravail, HeureDebutDefaut);
        var limite = ParserHeure(p?.HeureLimiteTolerance, HeureLimiteDefaut);
        if (limite < debut)
            limite = debut;
        return (debut, limite);
    }

    public static TimeSpan LireHeureFin(ParametresApplication? p)
    {
        var (debut, _) = LireHoraires(p);
        var fin = ParserHeure(p?.HeureFinTravail, HeureFinDefaut);
        return fin < debut ? debut : fin;
    }
}
