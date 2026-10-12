using MelodyPresence.Models;
using MelodyPresence.Services;
using Xunit;

namespace MelodyPresence.Tests;

public class RubriquesAPayerLtTests
{
    [Fact]
    public void Contient_les_12_rubriques_du_modele_Word()
    {
        Assert.Equal(12, RubriquesAPayerLt.Ordre.Count);
        Assert.Contains(RubriquesAPayerLt.SalaireBase, RubriquesAPayerLt.Ordre);
    }

    [Fact]
    public void En_cours_prorata_jours_ouvres_meme_sans_pointage()
    {
        var emp = new Employe { SalaireMensuel = 260000m }; // → taux 10000
        var lignes = RubriquesAPayerLt.Construire(emp, joursPointes: 0, periodeClose: false, joursOuvresEcoules: 9);
        var baseL = lignes.First(l => l.Libelle == RubriquesAPayerLt.SalaireBase);
        Assert.Equal(9m, baseL.Temps);
        Assert.Equal(10000m, baseL.Taux);
        Assert.Equal(90000m, baseL.Montant);
        Assert.Equal(0m, lignes.First(l => l.Libelle == RubriquesAPayerLt.Transport).Temps);
    }

    [Fact]
    public void Periode_close_utilise_26_jours_fixes()
    {
        var emp = new Employe { TauxSalaireBase = 8.90m, TauxTransport = 2.40m };
        var lignes = RubriquesAPayerLt.Construire(emp, joursPointes: 24, periodeClose: true, joursOuvresEcoules: 22);
        Assert.Equal(26m, lignes.First(l => l.Libelle == RubriquesAPayerLt.SalaireBase).Temps);
        Assert.Equal(24m, lignes.First(l => l.Libelle == RubriquesAPayerLt.Transport).Temps);
    }

    [Fact]
    public void CompleterTaux_depuis_salaire_mensuel()
    {
        var emp = new Employe { SalaireMensuel = 737620m };
        RubriquesAPayerLt.CompleterTauxSiBesoin(emp);
        Assert.Equal(28370m, Math.Round(emp.TauxSalaireBase, 0)); // 737620/26
    }

    [Fact]
    public void Indemnite_KM_utilise_jours_si_taux_sans_km()
    {
        var emp = new Employe { TauxSalaireBase = 10m, TauxIndemniteKm = 1.25m };
        var lignes = RubriquesAPayerLt.Construire(emp, joursPointes: 20, periodeClose: true);
        var km = lignes.First(l => l.Libelle == RubriquesAPayerLt.IndemniteKm);
        Assert.Equal(26m, km.Temps);
        Assert.Equal(1.25m, km.Taux);
        Assert.Equal(32.50m, km.Montant);
    }

    [Fact]
    public void Prime_assiduite_temps_egal_1()
    {
        var emp = new Employe { TauxSalaireBase = 10m, TauxPrimeAssiduite = 50m };
        var lignes = RubriquesAPayerLt.Construire(emp, joursPointes: 20, periodeClose: true);
        var prime = lignes.First(l => l.Libelle == RubriquesAPayerLt.PrimeAssiduite);
        Assert.Equal(1m, prime.Temps);
        Assert.Equal(50m, prime.Taux);
        Assert.Equal(50m, prime.Montant);
    }
}

public class PeriodePaieRealtimeTests
{
    [Fact]
    public void Le_6_octobre_fin_effective_est_aujourdhui()
    {
        var b = PeriodePaieLtService.ObtenirBornes(2026, 10);
        var fin = PeriodePaieLtService.FinEffective(b, new DateTime(2026, 10, 6));
        Assert.Equal(new DateTime(2026, 10, 6), fin);
        Assert.False(PeriodePaieLtService.EstCloturee(b, new DateTime(2026, 10, 6)));
    }

    [Fact]
    public void Apres_le_24_periode_cloturee()
    {
        var b = PeriodePaieLtService.ObtenirBornes(2026, 10);
        Assert.True(PeriodePaieLtService.EstCloturee(b, new DateTime(2026, 10, 24)));
        Assert.Equal(new DateTime(2026, 10, 23), PeriodePaieLtService.FinEffective(b, new DateTime(2026, 10, 30)));
    }
}
