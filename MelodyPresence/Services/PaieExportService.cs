using System.Globalization;
using System.IO;
using System.Text;
using MelodyPresence.Models;

namespace MelodyPresence.Services;

/// <summary>
/// Export des heures de présence au format d'échange Melody Paie RDC.
/// </summary>
public static class PaieExportService
{
    public static void ExporterHeuresMoisCsv(
        string chemin,
        string nomEntreprise,
        int annee,
        int mois,
        IReadOnlyList<ResumeMoisItem> lignes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Entreprise;Annee;Mois;Matricule;NomComplet;JoursPresents;HeuresTotales;Absences;Retards;Source");
        var inv = CultureInfo.InvariantCulture;
        foreach (var l in lignes)
        {
            sb.Append(Echapper(nomEntreprise)).Append(';')
                .Append(annee).Append(';')
                .Append(mois).Append(';')
                .Append(Echapper(l.Matricule)).Append(';')
                .Append(Echapper(l.NomComplet)).Append(';')
                .Append(l.JoursPresents).Append(';')
                .Append(l.HeuresTotales.ToString("0.##", inv)).Append(';')
                .Append(l.Absences).Append(';')
                .Append(l.Retards).Append(';')
                .AppendLine("LTPresence");
        }

        File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static void ExporterPointagesCsv(
        string chemin,
        IEnumerable<Pointage> pointages,
        IReadOnlyDictionary<int, Employe> employes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Matricule;CodePinZk;NomComplet;Horodatage;Type;Source");
        foreach (var p in pointages.OrderBy(x => x.Horodatage))
        {
            employes.TryGetValue(p.EmployeId, out var emp);
            sb.Append(Echapper(emp?.Matricule ?? "")).Append(';')
                .Append(Echapper(emp?.CodePinZk ?? "")).Append(';')
                .Append(Echapper(emp?.NomComplet ?? "")).Append(';')
                .Append(p.Horodatage.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(';')
                .Append(p.TypeLibelle).Append(';')
                .AppendLine(p.SourceLibelle);
        }

        File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Echapper(string? value)
    {
        var v = value ?? "";
        if (v.Contains('"') || v.Contains(';') || v.Contains('\n') || v.Contains('\r'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
