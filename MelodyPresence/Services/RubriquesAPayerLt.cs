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
    /// Complète le taux journalier de base si manquant (mensuel ÷ 26).
    /// Ne force plus d’indemnité KM (évite d’écrire 1,25 en base à chaque bulletin).
    /// </summary>
    public static void CompleterTauxSiBesoin(Employe emp)
    {
        if (emp.SalaireMensuel <= 0)
            return;
        if (emp.TauxSalaireBase <= 0)
            emp.TauxSalaireBase = decimal.Round(emp.SalaireMensuel / JoursReference, 2, MidpointRounding.AwayFromZero);
    }

    /// <param name="joursPointes">Jours réellement pointés (lun–ven) → rubrique Transport uniquement.</param>
    /// <param name="joursOuvresEcoules">Jours payables Mon–Sam écoulés (prorata ; samedi chômé payé inclus).</param>
    /// <param name="periodeClose">Si true, Temps fixes = 26 (modèle LT clôturé).</param>
    /// <param name="km">Km saisis pour la période ; sinon <see cref="Employe.KmMensuel"/>.</param>
    public static List<LigneBulletinAPayer> Construire(
        Employe emp,
        int joursPointes,
        bool periodeClose = false,
        int joursOuvresEcoules = 0,
        decimal? km = null)
    {
        CompleterTauxSiBesoin(emp);

        var pointes = Math.Max(0, joursPointes);
        var ouvrés = Math.Max(0, joursOuvresEcoules);
        if (ouvrés <= 0 && !periodeClose)
            ouvrés = pointes;

        // Période close : 26 j fixes. En cours : prorata jours payables écoulés (sam. inclus).
        var joursFixes = periodeClose
            ? JoursReference
            : (ouvrés > 0 ? ouvrés : pointes);
        // Jamais forcer 26 j sur une période pas commencée / sans jours écoulés
        if (joursFixes <= 0 && periodeClose && emp.TauxSalaireBase > 0)
            joursFixes = JoursReference;

        // Transport = jours pointés seulement (pas le samedi chômé payé)
        var joursTransport = pointes;

        var tauxBase = emp.TauxSalaireBase;
        var tauxMaladie = emp.TauxJourMaladie > 0
            ? emp.TauxJourMaladie
            : (tauxBase > 0 ? decimal.Round(tauxBase * 2m / 3m, 2, MidpointRounding.AwayFromZero) : 0m);
        var tauxFerie = emp.TauxJourFerie > 0
            ? emp.TauxJourFerie
            : (tauxBase > 0
                ? decimal.Round(tauxBase + emp.TauxAnciennete + emp.TauxTransport, 2, MidpointRounding.AwayFromZero)
                : 0m);
        var tauxHeureSup = emp.TauxHeureSup > 0
            ? emp.TauxHeureSup
            : (tauxBase > 0 ? decimal.Round(tauxBase / 8m, 2, MidpointRounding.AwayFromZero) : 0m);
        var tauxConge = emp.TauxCongeCirconstance > 0 ? emp.TauxCongeCirconstance : tauxBase;

        // KM : taux par défaut uniquement en mémoire si des km sont saisis
        var kmEffectif = km ?? emp.KmMensuel;
        var tauxKm = emp.TauxIndemniteKm;
        if (kmEffectif > 0 && tauxKm <= 0)
            tauxKm = 1.25m;

        var joursMaladie = emp.JoursMaladieMois;
        var heuresSup = emp.HeuresSupMois;
        var joursFeries = emp.JoursFeriesMois;
        var joursConge = emp.JoursCongeCirconstanceMois;

        // Rubriques journalières / forfait : Temps = joursFixes dès qu’un taux est renseigné
        var tempsAnciennete = emp.TauxAnciennete > 0 ? Math.Max(joursFixes, 1) : 0m;
        var tempsPrime = emp.TauxPrimeAssiduite > 0 ? Math.Max(joursFixes, 1) : 0m;
        var tempsComplement = emp.TauxComplementTransport > 0 ? Math.Max(joursFixes, 1) : 0m;
        var tempsLogement = emp.TauxLogement > 0 ? Math.Max(joursFixes, 1) : 0m;
        var tempsAlloc = emp.TauxAllocFamiliales > 0 ? Math.Max(joursFixes, 1) : 0m;

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
            Ligne(Anciennete, tempsAnciennete, emp.TauxAnciennete),
            Ligne(Transport, joursTransport, emp.TauxTransport),
            Ligne(JourMaladie, joursMaladie, tauxMaladie),
            Ligne(HeureSup, heuresSup, tauxHeureSup),
            Ligne(JourFeriePreste, joursFeries, tauxFerie),
            Ligne(PrimeAssiduite, tempsPrime, emp.TauxPrimeAssiduite),
            Ligne(IndemniteKm, kmEffectif, tauxKm),
            Ligne(CongeCirconstance, joursConge, tauxConge),
            Ligne(ComplementTransport, tempsComplement, emp.TauxComplementTransport),
            Ligne(IndemniteLogement, tempsLogement, emp.TauxLogement),
            Ligne(AllocationsFamiliales, tempsAlloc, emp.TauxAllocFamiliales)
        ];
    }
}
