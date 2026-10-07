namespace MelodyPresence.Services;

/// <summary>
/// Règle LT : 3 retards / mois sans retenue ; dès le 4e = moitié du salaire journalier.
/// </summary>
public static class RetardSanctionService
{
    public const int NbRetardsSansSanction = 3;
    public const int JoursReferenceMois = 26;

    public static decimal SalaireJournalier(decimal salaireMensuel) =>
        salaireMensuel <= 0
            ? 0m
            : decimal.Round(salaireMensuel / JoursReferenceMois, 2, MidpointRounding.AwayFromZero);

    public static int NbRetardsSanctionnes(int nbRetardsMois) =>
        Math.Max(0, nbRetardsMois - NbRetardsSansSanction);

    /// <summary>Montant total de retenue retards pour le mois.</summary>
    public static decimal CalculerRetenue(decimal salaireMensuel, int nbRetardsMois)
    {
        var n = NbRetardsSanctionnes(nbRetardsMois);
        if (n <= 0 || salaireMensuel <= 0)
            return 0m;

        var demiJour = SalaireJournalier(salaireMensuel) / 2m;
        return decimal.Round(n * demiJour, 2, MidpointRounding.AwayFromZero);
    }

    public static string LibelleRegle =>
        "3 retards / mois sans retenue ; dès le 4e : −½ journée (salaire mensuel ÷ 26 ÷ 2).";
}
