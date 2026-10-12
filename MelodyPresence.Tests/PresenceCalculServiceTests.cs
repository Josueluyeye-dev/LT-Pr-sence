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
        // Lundi 5 oct 2026 (jour ouvré, pas week-end)
        var ligne = PresenceCalculService.CalculerJour(emp, Array.Empty<Pointage>(), new DateTime(2026, 10, 5));
        Assert.Equal("Absent", ligne.Statut);
        Assert.Equal(0, ligne.Heures);
        Assert.Null(ligne.PremiereEntree);
    }

    [Fact]
    public void CalculerJour_arrivee_avant_7h40_est_parti()
    {
        var jour = new DateTime(2026, 10, 5);
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Test" };
        var pts = new[]
        {
            new Pointage { EmployeId = 1, Horodatage = jour.AddHours(7).AddMinutes(35), Type = PointageType.Entree },
            new Pointage { EmployeId = 1, Horodatage = jour.AddHours(17), Type = PointageType.Sortie }
        };
        var ligne = PresenceCalculService.CalculerJour(emp, pts, jour);
        Assert.Equal("Parti", ligne.Statut);
        Assert.False(ligne.EstEnRetard);
        Assert.Equal(0, ligne.MinutesRetard);
    }

    [Fact]
    public void CalculerJour_arrivee_apres_7h40_est_retard()
    {
        var jour = new DateTime(2026, 10, 5);
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Test" };
        var pts = new[]
        {
            new Pointage { EmployeId = 1, Horodatage = jour.AddHours(7).AddMinutes(41), Type = PointageType.Entree },
            new Pointage { EmployeId = 1, Horodatage = jour.AddHours(17), Type = PointageType.Sortie }
        };
        var ligne = PresenceCalculService.CalculerJour(emp, pts, jour);
        Assert.Equal("Retard", ligne.Statut);
        Assert.True(ligne.EstEnRetard);
        Assert.Equal(11, ligne.MinutesRetard); // depuis 07:30
    }

    [Fact]
    public void CalculerJour_une_seule_entree_a_lheure_est_en_cours()
    {
        var jour = DateTime.Today;
        var emp = new Employe { Id = 2, Matricule = "E2", Nom = "Solo" };
        var pts = new[]
        {
            new Pointage { EmployeId = 2, Horodatage = jour.Date.AddHours(7).AddMinutes(30), Type = PointageType.Entree }
        };
        var ligne = PresenceCalculService.CalculerJour(emp, pts, jour);
        Assert.Equal("En cours", ligne.Statut);
        Assert.Null(ligne.DerniereSortie);
        Assert.False(ligne.EstEnRetard);
    }

    [Fact]
    public void ResumeMensuel_compte_retards_et_presents()
    {
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Dupont", Actif = true };
        var pts = new[]
        {
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 5, 7, 35, 0) },
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 5, 16, 0, 0) },
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 6, 8, 0, 0) },
            new Pointage { EmployeId = 1, Horodatage = new DateTime(2026, 10, 6, 12, 0, 0) }
        };
        // Période figée : 2 jours pointés + samedis chômés payés jusqu’au 12/10
        var resume = PresenceCalculService.ResumeMensuel(
            new[] { emp }, pts, 2026, 10, aujourdhui: new DateTime(2026, 10, 12));
        Assert.Single(resume);
        // 2 jours pointés + 3 samedis (26/09, 03/10, 10/10)
        Assert.Equal(5, resume[0].JoursPresents);
        Assert.Equal(2, resume[0].JoursPointes);
        Assert.Equal(1, resume[0].Retards);
    }

    [Fact]
    public void Samedi_sans_pointage_est_chome_paye()
    {
        var samedi = new DateTime(2026, 10, 10); // samedi
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Test" };
        var ligne = PresenceCalculService.CalculerJour(emp, Array.Empty<Pointage>(), samedi);
        Assert.Equal("Chômé payé", ligne.Statut);
        Assert.Equal(0, ligne.Heures);
    }

    [Fact]
    public void ResumeMensuel_compte_samedi_chome_paye_comme_present()
    {
        var emp = new Employe { Id = 1, Matricule = "E1", Nom = "Dupont", Actif = true };
        // Période paie oct 2026 = 24/09 → 24/10 ; on fige « aujourd’hui » au 12/10
        var resume = PresenceCalculService.ResumeMensuel(
            new[] { emp }, Array.Empty<Pointage>(), 2026, 10,
            aujourdhui: new DateTime(2026, 10, 12));
        Assert.Single(resume);
        // Samedis 26/09, 03/10, 10/10 = 3 chômés payés ; lun–ven sans pointage = absences
        Assert.Equal(3, resume[0].JoursPresents);
        Assert.Equal(0, resume[0].JoursPointes);
        Assert.True(resume[0].Absences > 0);
    }
}
