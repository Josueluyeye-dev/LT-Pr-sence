using System.Text.Json;
using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public static class CalculBulletinService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static BulletinPaie Calculer(
        Employe employe,
        int annee,
        int mois,
        int joursPresents,
        int absences,
        int nbRetards,
        bool periodeClose = false,
        int joursOuvresEcoules = 0,
        IReadOnlyList<LigneBulletinRetenue>? retenuesSaisies = null)
    {
        RubriquesAPayerLt.CompleterTauxSiBesoin(employe);
        var lignes = RubriquesAPayerLt.Construire(employe, joursPresents, periodeClose, joursOuvresEcoules);
        var totalAPayer = decimal.Round(lignes.Sum(l => l.Montant), 2, MidpointRounding.AwayFromZero);

        var ligneBase = lignes.FirstOrDefault(l => l.Libelle == RubriquesAPayerLt.SalaireBase);
        var salaireBaseMensuel = ligneBase?.Montant ?? employe.SalaireMensuel;
        if (salaireBaseMensuel <= 0)
            salaireBaseMensuel = totalAPayer > 0 ? totalAPayer : employe.SalaireMensuel;

        var basePourSanction = employe.TauxSalaireBase > 0
            ? employe.TauxSalaireBase * RubriquesAPayerLt.JoursReference
            : employe.SalaireMensuel > 0 ? employe.SalaireMensuel : salaireBaseMensuel;

        var jour = RetardSanctionService.SalaireJournalier(basePourSanction);
        var sanctionnes = RetardSanctionService.NbRetardsSanctionnes(nbRetards);
        var retenueRetards = RetardSanctionService.CalculerRetenue(basePourSanction, nbRetards);

        var lignesRet = (retenuesSaisies ?? []).Where(r => r.Montant > 0).ToList();
        var totalRetenues = decimal.Round(lignesRet.Sum(r => r.Montant), 2, MidpointRounding.AwayFromZero);
        var net = decimal.Round(
            Math.Max(0m, totalAPayer - retenueRetards - totalRetenues),
            2,
            MidpointRounding.AwayFromZero);

        return new BulletinPaie
        {
            EmployeId = employe.Id,
            Employe = employe,
            Annee = annee,
            Mois = mois,
            Numero = $"BLT-{annee}{mois:D2}-{employe.Matricule}",
            DateGeneration = DateTime.Now,
            SalaireMensuel = salaireBaseMensuel,
            SalaireJournalier = jour,
            JoursPresents = joursPresents,
            Absences = absences,
            NbRetards = nbRetards,
            NbRetardsSanctionnes = sanctionnes,
            RetenueRetards = retenueRetards,
            TotalRetenues = totalRetenues,
            TotalAPayer = totalAPayer,
            NetAPayer = net,
            DetailAPayerJson = JsonSerializer.Serialize(lignes, JsonOpts),
            DetailRetenuesJson = JsonSerializer.Serialize(lignesRet, JsonOpts)
        };
    }

    public static IReadOnlyList<BulletinPaie> GenererMois(int annee, int mois, DateTime? aujourdhui = null)
    {
        var jourRef = aujourdhui ?? DateTime.Today;
        var bornes = PeriodePaieLtService.ObtenirBornes(annee, mois);
        var periodeClose = PeriodePaieLtService.EstCloturee(bornes, jourRef);
        var finCalc = PeriodePaieLtService.FinEffective(bornes, jourRef);
        var joursOuvres = finCalc >= bornes.Debut
            ? PeriodePaieLtService.CompterJoursOuvrables(bornes.Debut, finCalc)
            : 0;

        using var db = new PresenceDbContext();
        CompleterTauxManquants(db);

        var employes = db.Employes.Where(e => e.Actif).OrderBy(e => e.Nom).ToList();
        var finExclue = finCalc.AddDays(1);
        var pts = finCalc < bornes.Debut
            ? []
            : db.Pointages.AsNoTracking()
                .Where(p => p.Horodatage >= bornes.Debut && p.Horodatage < finExclue)
                .ToList();
        var (hDebut, hLimite) = PresenceCalculService.LireHoraires(
            db.Parametres.AsNoTracking().FirstOrDefault(x => x.Id == ParametresApplication.SingletonId));

        var resume = PresenceCalculService.ResumeMensuel(employes, pts, annee, mois, hDebut, hLimite, jourRef);
        var parId = resume.ToDictionary(r => r.Employe.Id);

        var retenues = db.Retenues.AsNoTracking()
            .Where(r => r.Annee == annee && r.Mois == mois && r.Montant > 0)
            .ToList()
            .GroupBy(r => r.EmployeId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new LigneBulletinRetenue
                {
                    Type = r.Type,
                    Libelle = string.IsNullOrWhiteSpace(r.LibelleLibre)
                        ? TypesRetenue.Libelle(r.Type)
                        : r.LibelleLibre!,
                    Montant = r.Montant
                }).ToList());

        foreach (var emp in employes)
        {
            parId.TryGetValue(emp.Id, out var r);
            retenues.TryGetValue(emp.Id, out var retEmp);
            var bulletin = Calculer(
                emp, annee, mois, r.JoursPresents, r.Absences, r.Retards,
                periodeClose, joursOuvres, retEmp);
            bulletin.Employe = null;

            var existant = db.Bulletins.FirstOrDefault(b =>
                b.EmployeId == emp.Id && b.Annee == annee && b.Mois == mois);
            if (existant != null)
            {
                existant.Numero = bulletin.Numero;
                existant.DateGeneration = bulletin.DateGeneration;
                existant.SalaireMensuel = bulletin.SalaireMensuel;
                existant.SalaireJournalier = bulletin.SalaireJournalier;
                existant.JoursPresents = bulletin.JoursPresents;
                existant.Absences = bulletin.Absences;
                existant.NbRetards = bulletin.NbRetards;
                existant.NbRetardsSanctionnes = bulletin.NbRetardsSanctionnes;
                existant.RetenueRetards = bulletin.RetenueRetards;
                existant.TotalRetenues = bulletin.TotalRetenues;
                existant.TotalAPayer = bulletin.TotalAPayer;
                existant.NetAPayer = bulletin.NetAPayer;
                existant.DetailAPayerJson = bulletin.DetailAPayerJson;
                existant.DetailRetenuesJson = bulletin.DetailRetenuesJson;
            }
            else
            {
                db.Bulletins.Add(bulletin);
            }
        }

        db.SaveChanges();

        return db.Bulletins.AsNoTracking()
            .Include(b => b.Employe)
            .Where(b => b.Annee == annee && b.Mois == mois)
            .OrderBy(b => b.Employe!.Nom)
            .ToList();
    }

    public static IReadOnlyList<BulletinPaie> ListerMois(int annee, int mois)
    {
        using var db = new PresenceDbContext();
        return db.Bulletins.AsNoTracking()
            .Include(b => b.Employe)
            .Where(b => b.Annee == annee && b.Mois == mois)
            .OrderBy(b => b.Employe!.Nom)
            .ToList();
    }

    /// <summary>Remplit TauxSalaireBase = SalaireMensuel÷26 si manquant.</summary>
    public static void CompleterTauxManquants(PresenceDbContext db)
    {
        var aCorriger = db.Employes
            .Where(e => e.SalaireMensuel > 0 && e.TauxSalaireBase <= 0)
            .ToList();
        if (aCorriger.Count == 0)
            return;
        foreach (var e in aCorriger)
            RubriquesAPayerLt.CompleterTauxSiBesoin(e);
        db.SaveChanges();
    }
}
