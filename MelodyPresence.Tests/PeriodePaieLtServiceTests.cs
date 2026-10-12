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

    [Fact]
    public void CompterJours_inclut_samedi_exclut_dimanche()
    {
        // Ven 9 oct 2026 → Dim 11 oct 2026 : ven + sam = 2 (dimanche exclu)
        var n = PeriodePaieLtService.CompterJoursOuvrables(
            new DateTime(2026, 10, 9), new DateTime(2026, 10, 11));
        Assert.Equal(2, n);
        Assert.True(PeriodePaieLtService.EstSamediChomePaye(new DateTime(2026, 10, 10)));
    }

    [Fact]
    public void PeriodePrecedente_depuis_octobre()
    {
        var b = PeriodePaieLtService.PeriodePrecedente(2026, 10);
        Assert.Equal(9, b.MoisPaiement);
        Assert.Equal(2026, b.AnneePaiement);
        Assert.Equal(new DateTime(2026, 8, 24), b.Debut);
        Assert.Equal(new DateTime(2026, 9, 24), b.Fin);
    }

    [Fact]
    public void Le_24_n_est_pas_dans_deux_periodes()
    {
        var oct = PeriodePaieLtService.ObtenirBornes(2026, 10);
        var nov = PeriodePaieLtService.ObtenirBornes(2026, 11);
        var j24 = new DateTime(2026, 10, 24);
        Assert.False(PeriodePaieLtService.Contient(oct, j24));
        Assert.True(PeriodePaieLtService.Contient(nov, j24));
        Assert.Equal(new DateTime(2026, 10, 23), PeriodePaieLtService.DernierJourInclus(oct));
    }
}
