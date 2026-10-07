using MelodyPresence.Services;
using Xunit;

namespace MelodyPresence.Tests;

public class PeriodePaieLtServiceTests
{
    [Fact]
    public void Octobre_2026_va_du_24_septembre_au_24_octobre_paye_le_25()
    {
        var b = PeriodePaieLtService.ObtenirBornes(2026, 10);
        Assert.Equal(new DateTime(2026, 9, 24), b.Debut);
        Assert.Equal(new DateTime(2026, 10, 24), b.Fin);
        Assert.Equal(new DateTime(2026, 10, 25), b.DatePaiement);
    }

    [Fact]
    public void Janvier_recule_sur_decembre_annee_precedente()
    {
        var b = PeriodePaieLtService.ObtenirBornes(2027, 1);
        Assert.Equal(new DateTime(2026, 12, 24), b.Debut);
        Assert.Equal(new DateTime(2027, 1, 24), b.Fin);
        Assert.Equal(new DateTime(2027, 1, 25), b.DatePaiement);
    }
}
