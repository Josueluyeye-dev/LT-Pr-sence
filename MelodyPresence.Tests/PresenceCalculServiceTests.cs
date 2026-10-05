using MelodyPresence.Models;
using MelodyPresence.Services;
using Xunit;

namespace MelodyPresence.Tests;

public class PresenceCalculServiceTests
{
    [Fact]
    public void CalculerJour_sans_pointage_est_absent()
    {
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Test" };
        var ligne = PresenceCalculService.CalculerJour(emp, Array.Empty<Pointage>(), DateTime.Today);
        Assert.Equal("Absent", ligne.Statut);
        Assert.Equal(0, ligne.Heures);
        Assert.Null(ligne.PremiereEntree);
    }

    [Fact]
    public void CalculerJour_avec_entree_et_sortie_calcule_heures()
    {
        var jour = new DateTime(2026, 10, 5);
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Test" };
        var pts = new[]
        {
            new Pointage { EmployeId = 1, Horodatage = jour.AddHours(8), Type = PointageType.Entree },
            new Pointage { EmployeId = 1, Horodatage = jour.AddHours(17), Type = PointageType.Sortie }
        };
        var ligne = PresenceCalculService.CalculerJour(emp, pts, jour);
        Assert.Equal("Présent", ligne.Statut);
        Assert.Equal(9, ligne.Heures);
        Assert.Equal(2, ligne.NbPointages);
    }

    [Fact]
    public void CalculerJour_une_seule_entree_est_en_cours()
    {
        var jour = DateTime.Today;
        var emp = new Employe { Id = 2, Matricule = "E2", Nom = "Solo" };
        var pts = new[]
        {
            new Pointage { EmployeId = 2, Horodatage = jour.AddHours(9), Type = PointageType.Entree }
        };
        var ligne = PresenceCalculService.CalculerJour(emp, pts, jour);
        Assert.Equal("En cours", ligne.Statut);
        Assert.Null(ligne.DerniereSortie);
    }

    [Fact]
    public void ResumeMensuel_agrege_jours_presents()
    {
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Dupont", Actif = true };
        // Lundi 5 octobre 2026
        var pts = new[]
        {
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 5, 8, 0, 0) },
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 5, 16, 0, 0) },
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 6, 8, 0, 0) },
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 6, 12, 0, 0) }
        };
        var resume = PresenceCalculService.ResumeMensuel(new[] { emp }, pts, 2026, 10);
        Assert.Single(resume);
        Assert.Equal(2, resume[0].JoursPresents);
        Assert.Equal(12, resume[0].HeuresTotales);
    }
}
