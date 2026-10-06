using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using ClosedXML.Excel;
using MelodyPresence.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MelodyPresence.Services;

public static class PresenceExportService
{
    private static readonly string BrandRed = "#E30613";
    private static readonly string BrandRedSoft = "#FFF1F2";
    private static readonly string Dark = "#1A1A1A";
    private static readonly string Ink = "#1F2937";
    private static readonly string Muted = "#6B7280";
    private static readonly string RowAlt = "#F8FAFC";
    private static readonly string Border = "#E5E7EB";
    private static readonly string Success = "#059669";
    private static readonly string Warning = "#D97706";
    private static readonly string Danger = "#DC2626";
    private static readonly string Info = "#2563EB";

    static PresenceExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void ExporterExcelResumeMois(
        string chemin,
        string entreprise,
        int annee,
        int mois,
        IReadOnlyList<(Employe Employe, int JoursPresents, double HeuresTotales, int Absences, int Retards)> lignes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Présence");
        ws.Cell(1, 1).Value = entreprise;
        ws.Cell(2, 1).Value = $"Résumé présence {mois:D2}/{annee}";
        ws.Cell(4, 1).Value = "Matricule";
        ws.Cell(4, 2).Value = "Employé";
        ws.Cell(4, 3).Value = "Jours présents";
        ws.Cell(4, 4).Value = "Heures";
        ws.Cell(4, 5).Value = "Absences";
        ws.Cell(4, 6).Value = "Retards";
        ws.Range(4, 1, 4, 6).Style.Font.Bold = true;

        var row = 5;
        foreach (var (emp, jours, heures, abs, retards) in lignes)
        {
            ws.Cell(row, 1).Value = emp.Matricule;
            ws.Cell(row, 2).Value = emp.NomComplet;
            ws.Cell(row, 3).Value = jours;
            ws.Cell(row, 4).Value = heures;
            ws.Cell(row, 5).Value = abs;
            ws.Cell(row, 6).Value = retards;
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(chemin);
    }

    public static void ExporterExcelDetailJour(
        string chemin,
        string entreprise,
        DateTime date,
        IReadOnlyList<JourPresenceLigne> lignes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Jour");
        ws.Cell(1, 1).Value = entreprise;
        ws.Cell(2, 1).Value = $"Présence du {date:dd/MM/yyyy}";
        ws.Cell(4, 1).Value = "Matricule";
        ws.Cell(4, 2).Value = "Employé";
        ws.Cell(4, 3).Value = "Entrée";
        ws.Cell(4, 4).Value = "Sortie";
        ws.Cell(4, 5).Value = "Heures";
        ws.Cell(4, 6).Value = "Statut";
        ws.Cell(4, 7).Value = "Retard (min)";
        ws.Range(4, 1, 4, 7).Style.Font.Bold = true;

        var row = 5;
        foreach (var l in lignes)
        {
            ws.Cell(row, 1).Value = l.Matricule;
            ws.Cell(row, 2).Value = l.NomComplet;
            ws.Cell(row, 3).Value = l.PremiereEntree?.ToString("HH:mm") ?? "";
            ws.Cell(row, 4).Value = l.DerniereSortie?.ToString("HH:mm") ?? "";
            ws.Cell(row, 5).Value = l.Heures;
            ws.Cell(row, 6).Value = l.Statut;
            ws.Cell(row, 7).Value = l.MinutesRetard;
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(chemin);
    }

    public static void ExporterCsvDetailJour(
        string chemin,
        string entreprise,
        DateTime date,
        IReadOnlyList<JourPresenceLigne> lignes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(entreprise);
        sb.AppendLine($"Présence du {date:dd/MM/yyyy}");
        sb.AppendLine("Matricule;Employé;Entrée;Sortie;Heures;Statut;Retard (min)");
        foreach (var l in lignes)
        {
            sb.Append(Csv(l.Matricule)).Append(';')
                .Append(Csv(l.NomComplet)).Append(';')
                .Append(l.PremiereEntree?.ToString("HH:mm") ?? "").Append(';')
                .Append(l.DerniereSortie?.ToString("HH:mm") ?? "").Append(';')
                .Append(l.Heures.ToString("0.##", CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(l.Statut)).Append(';')
                .Append(l.MinutesRetard)
                .AppendLine();
        }

        File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Csv(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    public static void ExporterPdfDetailJour(
        string chemin,
        string entreprise,
        DateTime date,
        IReadOnlyList<JourPresenceLigne> lignes)
    {
        var logo = ChargerLogo();
        var effectif = lignes.Count;
        var presents = lignes.Count(l => l.Statut is "Présent" or "Parti" or "En cours" or "Retard");
        var retards = lignes.Count(l => l.EstEnRetard || l.Statut == "Retard");
        var absents = lignes.Count(l => l.Statut is "Absent" or "Non pointé");
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var dateLibelle = date.ToString("dddd d MMMM yyyy", culture);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(0);
                page.MarginBottom(28);
                page.MarginHorizontal(0);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9).FontColor(Ink));

                page.Header().Element(h => ComposeEnTete(
                    h, logo, entreprise, "Rapport journalier de présence",
                    dateLibelle, "JOUR"));

                page.Content().PaddingHorizontal(36).PaddingTop(18).Column(col =>
                {
                    col.Item().Element(e => ComposeBandeauMeta(
                        e,
                        ("Période", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dateLibelle)),
                        ("Document", "Présence journalière"),
                        ("Généré le", DateTime.Now.ToString("dd/MM/yyyy HH:mm"))));

                    col.Item().PaddingTop(14).Element(e => ComposeKpis(e,
                        ("Effectif", effectif.ToString(), BrandRed),
                        ("Présents", presents.ToString(), Success),
                        ("Retards", retards.ToString(), Warning),
                        ("Absents", absents.ToString(), Danger)));

                    col.Item().PaddingTop(16).Text("Détail des pointages")
                        .SemiBold().FontSize(11).FontColor(Dark);

                    col.Item().PaddingTop(8).Element(e =>
                    {
                        e.Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.4f);
                                c.RelativeColumn(3.2f);
                                c.RelativeColumn(1.1f);
                                c.RelativeColumn(1.1f);
                                c.RelativeColumn(1.1f);
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "Matricule");
                                HeaderCell(header, "Employé");
                                HeaderCell(header, "Entrée");
                                HeaderCell(header, "Sortie");
                                HeaderCell(header, "Heures");
                                HeaderCell(header, "Statut");
                                HeaderCell(header, "Retard");
                            });

                            var i = 0;
                            foreach (var l in lignes.OrderBy(x => x.NomComplet))
                            {
                                var bg = i % 2 == 0 ? "#FFFFFF" : RowAlt;
                                BodyCell(table, l.Matricule, bg);
                                BodyCell(table, l.NomComplet, bg, bold: true);
                                BodyCell(table, l.EntreeAffichee, bg, align: Align.Center);
                                BodyCell(table, l.SortieAffichee, bg, align: Align.Center);
                                BodyCell(table, l.HeuresAffichees, bg, align: Align.Center);
                                StatutCell(table, l.StatutLibelle, bg);
                                BodyCell(table, l.RetardAffiche, bg, align: Align.Center,
                                    color: l.EstEnRetard ? Warning : Muted);
                                i++;
                            }
                        });
                    });

                    if (lignes.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Aucune donnée de présence pour cette date.")
                            .FontColor(Muted).Italic();
                    }
                });

                page.Footer().PaddingHorizontal(36).Element(f => ComposePied(f, entreprise));
            });
        }).GeneratePdf(chemin);
    }

    public static void ExporterPdfResumeMois(
        string chemin,
        string entreprise,
        int annee,
        int mois,
        IReadOnlyList<(Employe Employe, int JoursPresents, double HeuresTotales, int Absences, int Retards)> lignes)
    {
        var logo = ChargerLogo();
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var moisLibelle = culture.DateTimeFormat.GetMonthName(mois);
        var periode = $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(moisLibelle)} {annee}";
        var totalHeures = lignes.Sum(x => x.HeuresTotales);
        var totalAbs = lignes.Sum(x => x.Absences);
        var totalRetards = lignes.Sum(x => x.Retards);
        var totalPresents = lignes.Sum(x => x.JoursPresents);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(0);
                page.MarginBottom(28);
                page.MarginHorizontal(0);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9).FontColor(Ink));

                page.Header().Element(h => ComposeEnTete(
                    h, logo, entreprise, "Résumé mensuel de présence",
                    periode, "MOIS"));

                page.Content().PaddingHorizontal(36).PaddingTop(18).Column(col =>
                {
                    col.Item().Element(e => ComposeBandeauMeta(
                        e,
                        ("Période", periode),
                        ("Collaborateurs", lignes.Count.ToString()),
                        ("Généré le", DateTime.Now.ToString("dd/MM/yyyy HH:mm"))));

                    col.Item().PaddingTop(14).Element(e => ComposeKpis(e,
                        ("Jours présents", totalPresents.ToString(), Success),
                        ("Heures", totalHeures.ToString("0.#"), BrandRed),
                        ("Absences", totalAbs.ToString(), Danger),
                        ("Retards", totalRetards.ToString(), Warning)));

                    col.Item().PaddingTop(16).Text("Synthèse par collaborateur")
                        .SemiBold().FontSize(11).FontColor(Dark);

                    col.Item().PaddingTop(8).Element(e =>
                    {
                        e.Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(3.4f);
                                c.RelativeColumn(1.3f);
                                c.RelativeColumn(1.3f);
                                c.RelativeColumn(1.3f);
                                c.RelativeColumn(1.3f);
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "Matricule");
                                HeaderCell(header, "Employé");
                                HeaderCell(header, "Présents");
                                HeaderCell(header, "Heures");
                                HeaderCell(header, "Absences");
                                HeaderCell(header, "Retards");
                            });

                            var i = 0;
                            foreach (var (emp, jours, heures, abs, retards) in lignes.OrderBy(x => x.Employe.NomComplet))
                            {
                                var bg = i % 2 == 0 ? "#FFFFFF" : RowAlt;
                                BodyCell(table, emp.Matricule, bg);
                                BodyCell(table, emp.NomComplet, bg, bold: true);
                                BodyCell(table, jours.ToString(), bg, align: Align.Center);
                                BodyCell(table, heures.ToString("0.##"), bg, align: Align.Center);
                                BodyCell(table, abs.ToString(), bg, align: Align.Center,
                                    color: abs > 0 ? Danger : Ink);
                                BodyCell(table, retards.ToString(), bg, align: Align.Center,
                                    color: retards > 0 ? Warning : Ink);
                                i++;
                            }
                        });
                    });

                    if (lignes.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Aucune donnée pour ce mois.")
                            .FontColor(Muted).Italic();
                    }
                });

                page.Footer().PaddingHorizontal(36).Element(f => ComposePied(f, entreprise));
            });
        }).GeneratePdf(chemin);
    }

    private enum Align { Left, Center, Right }

    private static void ComposeEnTete(
        IContainer container,
        byte[]? logo,
        string entreprise,
        string titreDocument,
        string sousTitre,
        string badge)
    {
        container.Column(col =>
        {
            col.Item().Background(Colors.White).PaddingHorizontal(36).PaddingTop(18).PaddingBottom(14).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    if (logo != null)
                        c.Item().Height(52).Width(180).Image(logo).FitArea();
                    else
                    {
                        c.Item().Text(t =>
                        {
                            t.Span("LT").Bold().FontSize(20).FontColor(BrandRed);
                            t.Span("SERVICES").Bold().FontSize(20).FontColor(Dark);
                        });
                        c.Item().Text("vous êtes dans de bonnes mains")
                            .FontSize(8).FontColor(Muted).Italic();
                    }

                    c.Item().PaddingTop(8).Text(titreDocument)
                        .SemiBold().FontSize(13).FontColor(Dark);
                    c.Item().PaddingTop(2).Text(entreprise)
                        .FontSize(9).FontColor(Muted);
                });

                row.ConstantItem(130).AlignRight().AlignMiddle().Column(c =>
                {
                    c.Item().AlignRight().Background(BrandRed).PaddingHorizontal(12).PaddingVertical(5)
                        .Text(badge).Bold().FontSize(9).FontColor(Colors.White);
                    c.Item().PaddingTop(8).AlignRight()
                        .Text(sousTitre).SemiBold().FontSize(9).FontColor(Dark);
                    c.Item().PaddingTop(2).AlignRight()
                        .Text("Présence RH").FontSize(8).FontColor(Muted);
                });
            });

            col.Item().Height(3).Background(BrandRed);
            col.Item().Height(1).Background(Dark);
        });
    }

    private static void ComposeBandeauMeta(
        IContainer container,
        params (string Label, string Valeur)[] items)
    {
        container.Border(1).BorderColor(Border).Background(BrandRedSoft).Padding(12).Row(row =>
        {
            for (var i = 0; i < items.Length; i++)
            {
                var (label, valeur) = items[i];
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(label.ToUpperInvariant())
                        .FontSize(7.5f).FontColor(Muted).Bold().LetterSpacing(0.5f);
                    c.Item().PaddingTop(3).Text(valeur)
                        .SemiBold().FontSize(10).FontColor(Dark);
                });
                if (i < items.Length - 1)
                    row.ConstantItem(1).Background(Border).PaddingVertical(2);
            }
        });
    }

    private static void ComposeKpis(
        IContainer container,
        params (string Label, string Valeur, string Accent)[] kpis)
    {
        container.Row(row =>
        {
            for (var i = 0; i < kpis.Length; i++)
            {
                var (label, valeur, accent) = kpis[i];
                row.RelativeItem().PaddingRight(i < kpis.Length - 1 ? 8 : 0).Border(1).BorderColor(Border)
                    .Background(Colors.White).Column(c =>
                    {
                        c.Item().Height(3).Background(accent);
                        c.Item().Padding(10).Column(inner =>
                        {
                            inner.Item().Text(label.ToUpperInvariant())
                                .FontSize(7.5f).FontColor(Muted).Bold().LetterSpacing(0.4f);
                            inner.Item().PaddingTop(4).Text(valeur)
                                .Bold().FontSize(18).FontColor(Dark);
                        });
                    });
            }
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Background(Dark).PaddingVertical(7).PaddingHorizontal(6)
            .Text(text).Bold().FontSize(8).FontColor(Colors.White);
    }

    private static void BodyCell(
        TableDescriptor table,
        string text,
        string background,
        bool bold = false,
        Align align = Align.Left,
        string? color = null)
    {
        var cell = table.Cell().Background(background)
            .BorderBottom(0.6f).BorderColor(Border)
            .PaddingVertical(6).PaddingHorizontal(6);

        var t = align switch
        {
            Align.Center => cell.AlignCenter().Text(text),
            Align.Right => cell.AlignRight().Text(text),
            _ => cell.Text(text)
        };

        t.FontSize(8.5f).FontColor(color ?? Ink);
        if (bold) t.SemiBold();
    }

    private static void StatutCell(TableDescriptor table, string statut, string background)
    {
        var color = statut switch
        {
            "Présent" or "Parti" => Success,
            "En cours" => Info,
            "Retard" => Warning,
            "Non pointé" => Muted,
            _ => Danger
        };

        table.Cell().Background(background)
            .BorderBottom(0.6f).BorderColor(Border)
            .PaddingVertical(6).PaddingHorizontal(6)
            .AlignCenter()
            .Text(statut).SemiBold().FontSize(8).FontColor(color);
    }

    private static void ComposePied(IContainer container, string entreprise)
    {
        container.Column(col =>
        {
            col.Item().Height(2).Background(BrandRed);
            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(t =>
                {
                    t.Span("LT").SemiBold().FontColor(BrandRed).FontSize(8);
                    t.Span("SERVICES").SemiBold().FontColor(Dark).FontSize(8);
                    t.Span("  ·  ").FontColor(Muted).FontSize(8);
                    t.Span(entreprise).FontColor(Muted).FontSize(8);
                    t.Span("  ·  vous êtes dans de bonnes mains").FontColor(Muted).FontSize(8).Italic();
                });
                row.ConstantItem(90).AlignRight().AlignMiddle().Text(t =>
                {
                    t.Span("Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Dark).SemiBold();
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Dark).SemiBold();
                });
            });
        });
    }

    private static byte[]? ChargerLogo()
    {
        try
        {
            foreach (var name in new[]
                     {
                         "Assets/lt_services_brand.jpg",
                         "Assets/lt_services_logo.png",
                         "Assets/lt_services_icon.png"
                     })
            {
                var uri = new Uri($"pack://application:,,,/{name}");
                var info = Application.GetResourceStream(uri);
                if (info?.Stream == null) continue;
                using var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                if (ms.Length > 0)
                    return ms.ToArray();
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            foreach (var name in new[] { "lt_services_brand.jpg", "lt_services_logo.png", "lt_services_icon.png" })
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", name);
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
