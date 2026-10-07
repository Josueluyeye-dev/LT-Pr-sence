using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MelodyPresence.Models;

public class BulletinPaie
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe? Employe { get; set; }
    public int Annee { get; set; }
    public int Mois { get; set; }
    public string Numero { get; set; } = "";
    public DateTime DateGeneration { get; set; } = DateTime.Now;

    public decimal SalaireMensuel { get; set; }
    public decimal SalaireJournalier { get; set; }
    public int JoursPresents { get; set; }
    public int Absences { get; set; }
    public int NbRetards { get; set; }
    public int NbRetardsSanctionnes { get; set; }
    public decimal RetenueRetards { get; set; }

    /// <summary>Somme des retenues saisies (transport, prêts, CNSS, IPR…).</summary>
    public decimal TotalRetenues { get; set; }

    /// <summary>Somme des rubriques « A PAYER » (avant retenues).</summary>
    public decimal TotalAPayer { get; set; }
    public decimal NetAPayer { get; set; }

    /// <summary>JSON des lignes A PAYER (libelle, temps, taux, montant).</summary>
    public string DetailAPayerJson { get; set; } = "[]";

    /// <summary>JSON des retenues figées au moment de la génération.</summary>
    public string DetailRetenuesJson { get; set; } = "[]";

    [NotMapped]
    public IReadOnlyList<LigneBulletinAPayer> LignesAPayer
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<LigneBulletinAPayer>>(DetailAPayerJson)
                       ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    [NotMapped]
    public IReadOnlyList<LigneBulletinRetenue> LignesRetenues
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<LigneBulletinRetenue>>(DetailRetenuesJson)
                       ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    [NotMapped]
    public decimal TotalRetenuesAvecRetards => TotalRetenues + RetenueRetards;

    /// <summary>Période LT : 24→24 ; si ouverte, affiche jusqu’à aujourd’hui.</summary>
    [NotMapped]
    public string PeriodeLibelle
    {
        get
        {
            var bornes = Services.PeriodePaieLtService.ObtenirBornes(Annee, Mois);
            return Services.PeriodePaieLtService.LibelleSituation(bornes, DateGeneration.Date);
        }
    }
}

public sealed class LigneBulletinAPayer
{
    public string Libelle { get; set; } = "";
    public decimal Temps { get; set; }
    public decimal Taux { get; set; }
    public decimal Montant { get; set; }
}
