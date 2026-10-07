using System.Globalization;
using System.IO;
using System.Text;
using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public sealed class EmployeCsvImportResult
{
    public int Crees { get; set; }
    public int MisAJour { get; set; }
    public int Ignored { get; set; }
    public int LignesLues { get; set; }
}

/// <summary>
/// Import employés depuis CSV LT (Matricule;Nom;Postnom;Prenom;CodePinZk).
/// </summary>
public static class EmployeCsvImportService
{
    public static string? CheminSeedParDefaut()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidats = new[]
        {
            Path.Combine(baseDir, "Assets", "Seed", "lt_services_employes.csv"),
            Path.Combine(baseDir, "Seed", "lt_services_employes.csv"),
            Path.Combine(baseDir, "lt_services_employes.csv")
        };
        return candidats.FirstOrDefault(File.Exists);
    }

    public static EmployeCsvImportResult Importer(string cheminFichier, bool creerSeulementSiAbsent = false)
    {
        var result = new EmployeCsvImportResult();
        if (!File.Exists(cheminFichier))
            throw new FileNotFoundException("Fichier employés introuvable.", cheminFichier);

        var lignes = LireLignes(cheminFichier);
        result.LignesLues = lignes.Count;
        if (lignes.Count == 0)
            return result;

        using var db = new PresenceDbContext();
        var existants = db.Employes.ToList();
        var parMatricule = existants
            .Where(e => !string.IsNullOrWhiteSpace(e.Matricule))
            .ToDictionary(e => e.Matricule.Trim(), e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var ligne in lignes)
        {
            if (string.IsNullOrWhiteSpace(ligne.Matricule) || string.IsNullOrWhiteSpace(ligne.Nom))
            {
                result.Ignored++;
                continue;
            }

            var matricule = ligne.Matricule.Trim();
            var nom = string.IsNullOrWhiteSpace(ligne.Postnom)
                ? ligne.Nom.Trim()
                : $"{ligne.Nom.Trim()} {ligne.Postnom.Trim()}".Trim();
            var prenom = ligne.Prenom?.Trim() ?? "";
            var pin = string.IsNullOrWhiteSpace(ligne.CodePinZk) ? matricule : ligne.CodePinZk.Trim();

            if (parMatricule.TryGetValue(matricule, out var existant))
            {
                if (creerSeulementSiAbsent)
                {
                    result.Ignored++;
                    continue;
                }

                existant.Nom = nom;
                existant.Prenom = prenom;
                existant.CodePinZk = pin;
                existant.Actif = true;
                result.MisAJour++;
                continue;
            }

            var emp = new Employe
            {
                Matricule = matricule,
                Nom = nom,
                Prenom = prenom,
                CodePinZk = pin,
                Actif = true
            };
            db.Employes.Add(emp);
            parMatricule[matricule] = emp;
            result.Crees++;
        }

        db.SaveChanges();
        return result;
    }

    private static List<LigneCsv> LireLignes(string chemin)
    {
        var encoding = DetecterEncodage(chemin);
        var lines = File.ReadAllLines(chemin, encoding);
        if (lines.Length == 0)
            return new List<LigneCsv>();

        var sep = lines[0].Contains(';') ? ';' : ',';
        var header = Decouper(lines[0], sep);
        var idxMat = IndexCol(header, "Matricule", "MATRICULE");
        var idxNom = IndexCol(header, "Nom", "NOM");
        var idxPost = IndexCol(header, "Postnom", "POSTNOM");
        var idxPrenom = IndexCol(header, "Prenom", "Prénom", "PRENOM");
        var idxPin = IndexCol(header, "CodePinZk", "PIN", "CodePin");

        if (idxMat < 0 || idxNom < 0)
            throw new InvalidOperationException("CSV invalide : colonnes Matricule et Nom obligatoires.");

        var result = new List<LigneCsv>();
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = Decouper(line, sep);
            result.Add(new LigneCsv
            {
                Matricule = Cellule(cols, idxMat),
                Nom = Cellule(cols, idxNom),
                Postnom = Cellule(cols, idxPost),
                Prenom = Cellule(cols, idxPrenom),
                CodePinZk = Cellule(cols, idxPin)
            });
        }

        return result;
    }

    private static Encoding DetecterEncodage(string chemin)
    {
        var bytes = File.ReadAllBytes(chemin);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true);
        return new UTF8Encoding(false);
    }

    private static List<string> Decouper(string line, char sep)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (c == sep && !inQuotes)
            {
                cols.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        cols.Add(sb.ToString());
        return cols;
    }

    private static int IndexCol(IReadOnlyList<string> header, params string[] noms)
    {
        for (var i = 0; i < header.Count; i++)
        {
            var h = header[i].Trim().Trim('"');
            foreach (var n in noms)
            {
                if (string.Equals(h, n, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    private static string Cellule(IReadOnlyList<string> cols, int idx)
    {
        if (idx < 0 || idx >= cols.Count)
            return "";
        return cols[idx].Trim().Trim('"');
    }

    private sealed class LigneCsv
    {
        public string Matricule { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Postnom { get; set; } = "";
        public string Prenom { get; set; } = "";
        public string CodePinZk { get; set; } = "";
    }
}
