using System.Globalization;
using System.IO;
using System.Windows;
using MelodyPresence.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MelodyPresence.Services;

/// <summary>Export PDF des retenues (récap période + détail par employé).</summary>
public static class RetenuePdfService
{
    private static readonly string Red = "#E30613";
    private static readonly string Dark = "#0A0A0A";
    private static readonly string Ink = "#1A1A1A";
    private static readonly string Muted = "#5B6472";
    private static readonly string Border = "#CBD2DB";
    private static readonly string HeaderBg = "#111827";
    private static readonly string RowAlt = "#F3F4F6";

    static RetenuePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void ExporterPeriode(
        int annee,
        int mois,
        string entreprise,
        string chemin,
        IReadOnlyList<RetenuePaie> retenues)
    {
        var logo = ChargerLogo();
        var bornes = PeriodePaieLtService.ObtenirBornes(annee, mois);
        var periode = PeriodePaieLtService.LibelleCourt(bornes);
        var cult = CultureInfo.GetCultureInfo("fr-FR");
        var total = retenues.Sum(r => r.Montant);
        var parType = TypesRetenue.Tous
            .Select(t => (Libelle: t.Libelle, Total: retenues.Where(r => r.Type == t.Code).Sum(r => r.Montant)))
            .Where(x => x.Total > 0)
            .ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(18);
                page.MarginBottom(16);
                page.MarginHorizontal(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily("Segoe UI"));

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            if (logo != null)
                                c.Item().Height(56).MaxWidth(260).Image(logo).FitHeight();
                            else
                                c.Item().Text(entreprise.ToUpperInvariant()).Bold().FontSize(16).FontColor(Dark);
                            c.Item().PaddingTop(2).Text("vous êtes dans de bonnes mains")
                                .FontSize(8).FontColor(Muted).Italic();
                        });
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().Text("ÉTAT DES RETENUES").Bold().FontSize(13).FontColor(Red);
                            c.Item().PaddingTop(3).Text($"Paiement {mois:D2}/{annee}").FontSize(9).FontColor(Muted);
                            c.Item().PaddingTop(2).Text(periode).FontSize(8).FontColor(Dark).Bold();
                        });
                    });

                    col.Item().PaddingTop(8).Height(2).Background(Red);

                    col.Item().PaddingTop(10).Text($"Total retenues : {total.ToString("N2", cult)}")
                        .Bold().FontSize(12).FontColor(Dark);
                    col.Item().PaddingTop(2).Text($"{retenues.Count} ligne(s) · {retenues.Select(r => r.EmployeId).Distinct().Count()} employé(s)")
                        .FontSize(8.5f).FontColor(Muted);

                    if (parType.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("RÉCAPITULATIF PAR TYPE").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Border(1).BorderColor(Border).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.ConstantColumn(100);
                            });
                            table.Header(h =>
                            {
                                HCell(h.Cell(), "Type de retenue", false);
                                HCell(h.Cell(), "Montant", true);
                            });
                            var i = 0;
                            foreach (var (lib, mt) in parType)
                            {
                                var bg = i++ % 2 == 0 ? "#FFFFFF" : RowAlt;
                                BCell(table.Cell(), lib, bg, false);
                                BCell(table.Cell(), mt.ToString("N2", cult), bg, true);
                            }
                        });
                    }

                    col.Item().PaddingTop(12).Text("DÉTAIL PAR EMPLOYÉ").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Border(1).BorderColor(Border).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(2f);
                            c.ConstantColumn(80);
                        });
                        table.Header(h =>
                        {
                            HCell(h.Cell(), "Matricule", false);
                            HCell(h.Cell(), "Employé", false);
                            HCell(h.Cell(), "Retenue", false);
                            HCell(h.Cell(), "Montant", true);
                        });
                        var i = 0;
                        foreach (var r in retenues.OrderBy(x => x.Employe?.Nom).ThenBy(x => x.Type))
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : RowAlt;
                            BCell(table.Cell(), r.Employe?.Matricule ?? "—", bg, false);
                            BCell(table.Cell(), r.Employe?.NomComplet ?? "—", bg, false);
                            BCell(table.Cell(), r.TypeLibelle, bg, false);
                            BCell(table.Cell(), r.Montant.ToString("N2", cult), bg, true);
                        }

                        table.Cell().ColumnSpan(3).Background(Red).Padding(6)
                            .Text("TOTAL").FontColor(Colors.White).Bold();
                        table.Cell().Background(Red).Padding(6).AlignRight()
                            .Text(total.ToString("N2", cult)).FontColor(Colors.White).Bold();
                    });

                    col.Item().PaddingTop(14).AlignCenter()
                        .Text($"Édité le {DateTime.Now:dd/MM/yyyy HH:mm} — LT Services · IMPACT Entreprises")
                        .FontSize(7).FontColor(Muted).Italic();
                });
            });
        }).GeneratePdf(chemin);
    }

    private static void HCell(IContainer cell, string text, bool alignRight)
    {
        var c = cell.Background(HeaderBg).PaddingVertical(5).PaddingHorizontal(6).AlignMiddle();
        (alignRight ? c.AlignRight() : c).Text(text).FontColor(Colors.White).Bold().FontSize(8);
    }

    private static void BCell(IContainer cell, string text, string bg, bool alignRight)
    {
        var c = cell.Background(bg).BorderBottom(0.5f).BorderColor(Border)
            .PaddingVertical(3).PaddingHorizontal(6).AlignMiddle();
        (alignRight ? c.AlignRight() : c).Text(text).FontSize(8);
    }

    private static byte[]? ChargerLogo()
    {
        try
        {
            foreach (var name in new[] { "lt_services_brand.jpg", "lt_services_logo.png" })
            {
                var uri = new Uri($"pack://application:,,,/Assets/{name}");
                var info = Application.GetResourceStream(uri);
                if (info?.Stream == null) continue;
                using var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                if (ms.Length > 0) return ms.ToArray();
            }
        }
        catch { /* ignore */ }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "lt_services_brand.jpg");
            if (File.Exists(path)) return File.ReadAllBytes(path);
        }
        catch { /* ignore */ }

        return null;
    }
}
