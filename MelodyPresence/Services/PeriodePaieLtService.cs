namespace MelodyPresence.Services;

/// <summary>
/// Période de paie LT Services : du 24 au 24, paiement le 25.
/// Le mois/année choisis = mois du paiement (ex. 10/2026 → 24/09 → 24/10, payé le 25/10).
/// </summary>
public static class PeriodePaieLtService
{
    public const int JourDebut = 24;
    public const int JourFin = 24;
    public const int JourPaiement = 25;

    public readonly record struct Bornes(
        DateTime Debut,
        DateTime Fin,
        DateTime DatePaiement,
        int MoisPaiement,
        int AnneePaiement);

    public static Bornes ObtenirBornes(int anneePaiement, int moisPaiement)
    {
        moisPaiement = Math.Clamp(moisPaiement, 1, 12);
        var fin = SafeDate(anneePaiement, moisPaiement, JourFin);
        var moisDebut = moisPaiement == 1 ? 12 : moisPaiement - 1;
        var anneeDebut = moisPaiement == 1 ? anneePaiement - 1 : anneePaiement;
        var debut = SafeDate(anneeDebut, moisDebut, JourDebut);
        var paiement = SafeDate(anneePaiement, moisPaiement, JourPaiement);
        return new Bornes(debut, fin, paiement, moisPaiement, anneePaiement);
    }

    /// <summary>Période en cours = mois de paiement du calendrier (ex. 06/10 → paie octobre : 24/09→24/10).</summary>
    public static Bornes PeriodeCourante(DateTime? aujourdhui = null)
    {
        var d = (aujourdhui ?? DateTime.Today).Date;
        return ObtenirBornes(d.Year, d.Month);
    }

    /// <summary>Fin prise en compte pour un calcul « en cours » : pas de jours futurs.</summary>
    public static DateTime FinEffective(Bornes b, DateTime? aujourdhui = null)
    {
        var today = (aujourdhui ?? DateTime.Today).Date;
        if (today < b.Debut)
            return b.Debut.AddDays(-1); // période pas commencée
        return today < b.Fin ? today : b.Fin;
    }

    public static bool EstCloturee(Bornes b, DateTime? aujourdhui = null) =>
        (aujourdhui ?? DateTime.Today).Date >= b.Fin;

    public static int CompterJoursOuvrables(DateTime debut, DateTime finInclusive)
    {
        if (finInclusive < debut)
            return 0;
        var n = 0;
        for (var d = debut.Date; d <= finInclusive.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            n++;
        }

        return n;
    }

    public static string LibelleCourt(Bornes b) =>
        $"{b.Debut:dd/MM/yyyy} → {b.Fin:dd/MM/yyyy}";

    public static string LibelleComplet(Bornes b) =>
        $"{LibelleCourt(b)} · paiement le {b.DatePaiement:dd/MM/yyyy}";

    public static string LibelleSituation(Bornes b, DateTime? aujourdhui = null)
    {
        var today = (aujourdhui ?? DateTime.Today).Date;
        var finEff = FinEffective(b, today);
        if (EstCloturee(b, today))
            return LibelleComplet(b);
        return $"{b.Debut:dd/MM/yyyy} → {finEff:dd/MM/yyyy} (en cours · clôture {b.Fin:dd/MM/yyyy} · paie le {b.DatePaiement:dd/MM/yyyy})";
    }

    public static string LibellePaiement(int mois, int annee) =>
        $"Paie {mois:D2}/{annee} (du 24 au 24, payée le 25)";

    public static bool Contient(Bornes b, DateTime date)
    {
        var d = date.Date;
        return d >= b.Debut && d <= b.Fin;
    }

    private static DateTime SafeDate(int annee, int mois, int jour)
    {
        jour = Math.Min(jour, DateTime.DaysInMonth(annee, mois));
        return new DateTime(annee, mois, jour);
    }
}
