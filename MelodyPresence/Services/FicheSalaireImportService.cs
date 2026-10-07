using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MelodyPresence.Data;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Services;

public sealed class FicheSalaireImportResult
{
    public int MisAJour { get; set; }
    public int Crees { get; set; }
    public int NonMatchés { get; set; }
    public int LignesLues { get; set; }
    public List<string> Details { get; } = new();
}

/// <summary>
/// Import « FICHER DE SALAIRE » → SalaireMensuel + taux journaliers « A PAYER ».
/// </summary>
public static class FicheSalaireImportService
{
    public static FicheSalaireImportResult Importer(string cheminFichier)
    {
        var result = new FicheSalaireImportResult();
        if (!File.Exists(cheminFichier))
            throw new FileNotFoundException("Fichier introuvable.", cheminFichier);

        using var wb = new XLWorkbook(cheminFichier);
        var ws = wb.Worksheets.FirstOrDefault(w =>
                     w.Name.Contains("SALAIRE", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheets.First();

        var headerRow = TrouverEntete(ws);
        if (headerRow < 0)
            throw new InvalidOperationException("En-têtes introuvables (NOM / Salaire mensuel).");

        var colNom = TrouverColonne(ws, headerRow, "NOM & POST", "NOM");
        var colSal = TrouverColonne(ws, headerRow, "Salaire mensuel en FC", "Salaire mensuel");
        var colBaseJr = TrouverColonne(ws, headerRow, "SAL BASE/Jr", "SAL BASE");
        var colAnnuite = TrouverColonne(ws, headerRow, "ANNUITE/Jr", "ANNUITE");
        var colTransport = TrouverColonne(ws, headerRow, "TRANSPORT/JOUR", "TRANSPORT");
        var colLogement = TrouverColonne(ws, headerRow, "LOGEMENT/JOUR", "LOGEMENT");
        var colAlfa = TrouverColonne(ws, headerRow, "ALFA/JOUR", "ALFA");
        if (colNom < 0 || colSal < 0)
            throw new InvalidOperationException("Colonnes NOM ou Salaire mensuel manquantes.");

        var lignes = new List<(string Nom, decimal Salaire, decimal BaseJr, decimal Annuite, decimal Transport, decimal Logement, decimal Alfa)>();
        var last = ws.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var r = headerRow + 1; r <= last; r++)
        {
            var nom = ws.Cell(r, colNom).GetString().Trim();
            if (string.IsNullOrWhiteSpace(nom))
                continue;
            var sal = LireDecimal(ws.Cell(r, colSal));
            if (sal is null or <= 0)
                continue;
            lignes.Add((
                nom,
                sal.Value,
                colBaseJr > 0 ? LireDecimal(ws.Cell(r, colBaseJr)) ?? 0m : 0m,
                colAnnuite > 0 ? LireDecimal(ws.Cell(r, colAnnuite)) ?? 0m : 0m,
                colTransport > 0 ? LireDecimal(ws.Cell(r, colTransport)) ?? 0m : 0m,
                colLogement > 0 ? LireDecimal(ws.Cell(r, colLogement)) ?? 0m : 0m,
                colAlfa > 0 ? LireDecimal(ws.Cell(r, colAlfa)) ?? 0m : 0m));
        }

        result.LignesLues = lignes.Count;

        using var db = new PresenceDbContext();
        var employes = db.Employes.ToList();
        var used = new HashSet<int>();

        foreach (var (nomExcel, salaire, baseJr, annuite, transport, logement, alfa) in lignes)
        {
            var match = MeilleurMatch(nomExcel, employes.Where(e => !used.Contains(e.Id)));
            var cree = false;
            if (match == null)
            {
                // Installation neuve / liste vide : créer l'employé depuis la fiche salaire
                var (nom, prenom) = DecouperNomExcel(nomExcel);
                var matricule = GenererMatriculeTemporaire(nom, prenom, employes);
                match = new Employe
                {
                    Matricule = matricule,
                    Nom = nom,
                    Prenom = prenom,
                    CodePinZk = matricule,
                    Actif = true
                };
                db.Employes.Add(match);
                employes.Add(match);
                cree = true;
                result.Crees++;
            }

            used.Add(match.Id);
            match.SalaireMensuel = salaire;
            match.TauxSalaireBase = baseJr > 0
                ? baseJr
                : decimal.Round(salaire / RubriquesAPayerLt.JoursReference, 2, MidpointRounding.AwayFromZero);
            if (annuite > 0) match.TauxAnciennete = annuite;
            if (transport > 0) match.TauxTransport = transport;
            if (logement > 0) match.TauxLogement = logement;
            if (alfa > 0) match.TauxAllocFamiliales = alfa;
            result.MisAJour++;
            result.Details.Add(cree
                ? $"+ {match.Matricule} créé ← {nomExcel} = {salaire:N2}"
                : $"{match.Matricule} ← {nomExcel} = {salaire:N2}");
        }

        db.SaveChanges();
        return result;
    }

    private static (string Nom, string Prenom) DecouperNomExcel(string nomExcel)
    {
        var parts = nomExcel.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return ("Inconnu", "");
        if (parts.Length == 1)
            return (parts[0], "");
        if (parts.Length == 2)
            return (parts[0], parts[1]);
        // NOM POSTNOM PRENOM… → Nom = 2 premiers, Prenom = reste
        return (string.Join(' ', parts.Take(2)), string.Join(' ', parts.Skip(2)));
    }

    private static string GenererMatriculeTemporaire(string nom, string prenom, List<Employe> existants)
    {
        var baseMat = "TMP-" + new string((nom + prenom)
            .Where(char.IsLetterOrDigit)
            .Take(8)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (string.IsNullOrWhiteSpace(baseMat) || baseMat == "TMP-")
            baseMat = "TMP-EMP";
        var candidat = baseMat;
        var n = 1;
        var connus = new HashSet<string>(existants.Select(e => e.Matricule), StringComparer.OrdinalIgnoreCase);
        while (connus.Contains(candidat))
        {
            candidat = $"{baseMat}-{n}";
            n++;
        }

        return candidat;
    }

    public static string? CheminSeedSalaireParDefaut()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidats = new[]
        {
            Path.Combine(baseDir, "Assets", "Seed", "FICHER_DE_SALAIRE_LT.xlsx"),
            Path.Combine(baseDir, "Seed", "FICHER_DE_SALAIRE_LT.xlsx"),
            Path.Combine(baseDir, "FICHER_DE_SALAIRE_LT.xlsx")
        };
        return candidats.FirstOrDefault(File.Exists);
    }

    private static Employe? MeilleurMatch(string nomExcel, IEnumerable<Employe> candidats)
    {
        var xt = Tokens(nomExcel);
        if (xt.Count == 0) return null;
        Employe? best = null;
        var bestScore = 0.0;
        foreach (var e in candidats)
        {
            var dt = Tokens($"{e.Nom} {e.Prenom}");
            if (dt.Count == 0) continue;
            var inter = xt.Intersect(dt).Count();
            if (inter == 0) continue;
            var union = xt.Union(dt).Count();
            var jacc = inter / (double)union;
            var cover = inter / (double)Math.Max(xt.Count, dt.Count);
            var score = Math.Max(jacc, cover);
            if (xt.IsSubsetOf(dt) || dt.IsSubsetOf(xt))
                score = Math.Max(score, 0.85);
            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        return bestScore >= 0.55 ? best : null;
    }

    private static HashSet<string> Tokens(string s)
    {
        var n = Normalize(s);
        return n.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string s)
    {
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToUpperInvariant(ch));
            else
                sb.Append(' ');
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static int TrouverEntete(IXLWorksheet ws)
    {
        var lastR = Math.Min(40, ws.LastRowUsed()?.RowNumber() ?? 1);
        var lastC = Math.Min(40, ws.LastColumnUsed()?.ColumnNumber() ?? 1);
        for (var r = 1; r <= lastR; r++)
        {
            var parts = new List<string>();
            for (var c = 1; c <= lastC; c++)
            {
                var t = ws.Cell(r, c).GetString().Trim();
                if (!string.IsNullOrEmpty(t))
                    parts.Add(t);
            }

            var joined = string.Join(" | ", parts).ToUpperInvariant();
            if (joined.Contains("NOM") && joined.Contains("SALAIRE"))
                return r;
        }

        return -1;
    }

    private static int TrouverColonne(IXLWorksheet ws, int headerRow, params string[] needles)
    {
        var lastC = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        for (var c = 1; c <= lastC; c++)
        {
            var t = ws.Cell(headerRow, c).GetString().Trim();
            if (string.IsNullOrEmpty(t)) continue;
            foreach (var n in needles)
            {
                if (t.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
        }

        return -1;
    }

    private static decimal? LireDecimal(IXLCell cell)
    {
        try
        {
            if (cell.TryGetValue(out double d) && !double.IsNaN(d) && !double.IsInfinity(d))
                return (decimal)d;
        }
        catch
        {
            // formule / type
        }

        try
        {
            var cached = cell.CachedValue;
            if (cached.IsNumber)
                return (decimal)cached.GetNumber();
            if (cached.IsText)
            {
                var tt = cached.GetText().Trim().Replace(" ", "").Replace(",", ".");
                if (decimal.TryParse(tt, NumberStyles.Any, CultureInfo.InvariantCulture, out var vc))
                    return vc;
            }
        }
        catch
        {
            // ignore
        }

        var t = cell.GetString().Trim().Replace(" ", "").Replace(",", ".");
        if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }
}
