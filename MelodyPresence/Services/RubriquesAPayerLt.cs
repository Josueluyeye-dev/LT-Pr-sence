using MelodyPresence.Models;

namespace MelodyPresence.Services;

/// <summary>
/// Rubriques « A PAYER » du bulletin LT (modèle Word).
/// </summary>
public static class RubriquesAPayerLt
{
    public const int JoursReference = 26;

    public const string SalaireBase = "Salaire de base";
    public const string Anciennete = "Ancienneté";
    public const string Transport = "Transport";
    public const string JourMaladie = "Jour de maladie";
    public const string HeureSup = "Heure supplémentaire";
    public const string JourFeriePreste = "Jour férié presté";
    public const string PrimeAssiduite = "Prime d'Assiduité";
    public const string IndemniteKm = "Indemnité KM";
    public const string CongeCirconstance = "Congé de circonstance";
    public const string ComplementTransport = "Complément transport";
    public const string IndemniteLogement = "Indemnité logement";
    public const string AllocationsFamiliales = "Allocations familiales";

    public static IReadOnlyList<string> Ordre { get; } =
    [
        SalaireBase,
        Anciennete,
        Transport,
        JourMaladie,
        HeureSup,
        JourFeriePreste,
        PrimeAssiduite,
        IndemniteKm,
        CongeCirconstance,
        ComplementTransport,
        IndemniteLogement,
        AllocationsFamiliales
    ];

    /// <summary>
    /// Complète les taux manquants à partir du salaire mensuel (÷ 26).
    /// </summary>
    public static void CompleterTauxSiBesoin(Employe emp)
    {
        if (emp.SalaireMensuel <= 0)
            return;
        if (emp.TauxSalaireBase <= 0)
            emp.TauxSalaireBase = decimal.Round(emp.SalaireMensuel / JoursReference, 2, MidpointRounding.AwayFromZero);
    }

    /// <param name="joursPresents">Jours réellement pointés (transport).</param>
    /// <param name="joursOuvresEcoules">Jours ouvrés de la période jusqu’à aujourd’hui (prorata).</param>
    /// <param name="periodeClose">Si true, Temps fixes = 26 (modèle LT clôturé).</param>
    public static List<LigneBulletinAPayer> Construire(
        Employe emp,
        int joursPresents,
        bool periodeClose = false,
        int joursOuvresEcoules = 0,
        decimal km = 0m)
    {
        CompleterTauxSiBesoin(emp);

        var presents = Math.Max(0, joursPresents);
        var ouvrés = Math.Max(0, joursOuvresEcoules);
        if (ouvrés <= 0 && !periodeClose)
            ouvrés = presents;

        // Période close : 26 j fixes. En cours : prorata jours ouvrés écoulés (pas 0 si pas encore pointé).
        var joursFixes = periodeClose
            ? JoursReference
            : (ouvrés > 0 ? ouvrés : presents);
        var joursTransport = presents;

        var tauxBase = emp.TauxSalaireBase;
        var tauxMaladie = emp.TauxJourMaladie > 0
            ? emp.TauxJourMaladie
            : decimal.Round(tauxBase * 2m / 3m, 2, MidpointRounding.AwayFromZero);
        var tauxFerie = emp.TauxJourFerie > 0
            ? emp.TauxJourFerie
            : decimal.Round(tauxBase + emp.TauxAnciennete + emp.TauxTransport, 2, MidpointRounding.AwayFromZero);

        LigneBulletinAPayer Ligne(string lib, decimal temps, decimal taux) =>
            new()
            {
                Libelle = lib,
                Temps = temps,
                Taux = taux,
                Montant = decimal.Round(temps * taux, 2, MidpointRounding.AwayFromZero)
            };

        return
        [
            Ligne(SalaireBase, joursFixes, tauxBase),
            Ligne(Anciennete, joursFixes, emp.TauxAnciennete),
            Ligne(Transport, joursTransport, emp.TauxTransport),
            Ligne(JourMaladie, 0, tauxMaladie),
            Ligne(HeureSup, 0, 0),
            Ligne(JourFeriePreste, 0, tauxFerie),
            Ligne(PrimeAssiduite, 0, emp.TauxPrimeAssiduite),
            Ligne(IndemniteKm, km, emp.TauxIndemniteKm > 0 ? emp.TauxIndemniteKm : 1.25m),
            Ligne(CongeCirconstance, 0, 0),
            Ligne(ComplementTransport, joursFixes, emp.TauxComplementTransport),
            Ligne(IndemniteLogement, joursFixes, emp.TauxLogement),
            Ligne(AllocationsFamiliales, joursFixes, emp.TauxAllocFamiliales)
        ];
    }
}
