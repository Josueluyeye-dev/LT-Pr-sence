using System.Globalization;
using System.IO;
using System.Windows;
using MelodyPresence.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MelodyPresence.Services;

/// <summary>
/// Bulletin LT — 1 page A4, logo marque, sans bandeau rouge redondant.
/// </summary>
public static class BulletinPdfService
{
    private static readonly string Red = "#E30613";
    private static readonly string Dark = "#0A0A0A";
    private static readonly string Ink = "#1A1A1A";
    private static readonly string Muted = "#5B6472";
    private static readonly string Border = "#CBD2DB";
    private static readonly string HeaderBg = "#111827";
    private static readonly string RowAlt = "#F3F4F6";

    static BulletinPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void Exporter(BulletinPaie b, string entreprise, string chemin)
    {
        var logo = ChargerLogo();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(18);
                page.MarginBottom(16);
                page.MarginHorizontal(26);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily("Segoe UI"));
                page.Content().Element(c => Compose(c, b, entreprise, logo));
            });
        }).GeneratePdf(chemin);
    }

    public static void ExporterTous(IEnumerable<BulletinPaie> bulletins, string entreprise, string dossier)
    {
        Directory.CreateDirectory(dossier);
        foreach (var b in bulletins)
        {
            var nom = Sanitize($"{b.Numero}_{b.Employe?.Matricule}.pdf");
            Exporter(b, entreprise, Path.Combine(dossier, nom));
        }
    }

    private static void Compose(IContainer container, BulletinPaie b, string entreprise, byte[]? logo)
    {
        var emp = b.Employe;
        var cult = CultureInfo.GetCultureInfo("fr-FR");
        var lignes = b.LignesAPayer;
        if (lignes.Count == 0 && emp != null)
            lignes = RubriquesAPayerLt.Construire(emp, b.JoursPresents);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    if (logo != null)
                    {
                        c.Item().AlignLeft().Height(68).MaxWidth(300)
                            .Image(logo).FitHeight();
                    }
                    else
                    {
                        c.Item().Text(string.IsNullOrWhiteSpace(entreprise) ? "LT SERVICES" : entreprise.ToUpperInvariant())
                            .Bold().FontSize(18).FontColor(Dark);
                    }

                    c.Item().PaddingTop(3).Text("vous êtes dans de bonnes mains")
                        .FontSize(8).FontColor(Muted).Italic();
                });

                row.ConstantItem(180).AlignMiddle().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("BULLETIN DE PAIE").Bold().FontSize(13).FontColor(Red);
                    c.Item().AlignRight().PaddingTop(3).Text($"N° {b.Numero}").FontSize(8).FontColor(Muted);
                    c.Item().AlignRight().PaddingTop(4).Text(b.PeriodeLibelle)
                        .FontSize(7.5f).FontColor(Dark).Bold();
                });
            });

            col.Item().PaddingTop(8).Height(2).Background(Red);

            col.Item().PaddingTop(10).Border(1).BorderColor(Border).Row(row =>
            {
                row.ConstantItem(78).Background(HeaderBg).Padding(7).AlignMiddle()
                    .Text("IDENTITÉ").FontColor(Colors.White).Bold().FontSize(8);

                row.RelativeItem().PaddingVertical(7).PaddingHorizontal(10).Column(c =>
                {
                    c.Item().Text(emp?.NomComplet ?? "—").Bold().FontSize(11);
                    c.Item().PaddingTop(2).Text(
                            $"Matricule : {emp?.Matricule ?? "—"}   ·   Présents : {b.JoursPresents} j   ·   Absences : {b.Absences}   ·   Retards : {b.NbRetards}")
                        .FontSize(8.5f);
                    c.Item().PaddingTop(2).Text($"Édité le {b.DateGeneration:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(12).Text("A PAYER").Bold().FontSize(11).FontColor(Dark);

            col.Item().PaddingTop(4).Border(1).BorderColor(Border).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3.6f);
                    c.ConstantColumn(60);
                    c.ConstantColumn(70);
                    c.ConstantColumn(86);
                });

                table.Header(h =>
                {
                    HCell(h.Cell(), "Rubriques", false);
                    HCell(h.Cell(), "Temps", true);
                    HCell(h.Cell(), "Taux", true);
                    HCell(h.Cell(), "Montant", true);
                });

                var i = 0;
                foreach (var l in lignes)
                {
                    var bg = i++ % 2 == 0 ? "#FFFFFF" : RowAlt;
                    BCell(table.Cell(), l.Libelle, bg, false);
                    BCell(table.Cell(), l.Temps.ToString("0.##", cult), bg, true);
                    BCell(table.Cell(), l.Taux.ToString("N2", cult), bg, true);
                    BCell(table.Cell(), l.Montant.ToString("N2", cult), bg, true);
                }

                table.Cell().ColumnSpan(3).Background(Red).PaddingVertical(6).PaddingHorizontal(10)
                    .AlignMiddle().Text("TOTAL A PAYER").FontColor(Colors.White).Bold().FontSize(10);
                table.Cell().Background(Red).PaddingVertical(6).PaddingHorizontal(10)
                    .AlignMiddle().AlignRight()
                    .Text(b.TotalAPayer.ToString("N2", cult)).FontColor(Colors.White).Bold().FontSize(11);
            });

            if (b.RetenueRetards > 0)
            {
                col.Item().PaddingTop(5).Background("#FEF2F2").Border(1).BorderColor("#FECACA")
                    .PaddingVertical(5).PaddingHorizontal(8)
                    .Text($"Retenue retards : {b.NbRetards} retard(s) · {b.NbRetardsSanctionnes} sanctionné(s)  →  − {b.RetenueRetards.ToString("N2", cult)}")
                    .FontSize(8.5f).FontColor(Red).Bold();
            }

            col.Item().PaddingTop(8).Background(Dark).PaddingVertical(9).PaddingHorizontal(12).Row(row =>
            {
                row.RelativeItem().AlignMiddle()
                    .Text("NET À PAYER").FontColor(Colors.White).Bold().FontSize(11).LetterSpacing(0.4f);
                row.ConstantItem(130).AlignMiddle().AlignRight()
                    .Text(b.NetAPayer.ToString("N2", cult)).FontColor(Colors.White).Bold().FontSize(15);
            });

            col.Item().PaddingTop(14).Height(1).Background(Border);
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Signature de l'employé").FontSize(8).FontColor(Muted).Bold();
                    c.Item().PaddingTop(26).BorderBottom(1).BorderColor(Dark);
                });
                row.ConstantItem(24);
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Signature de l'employeur").FontSize(8).FontColor(Muted).Bold();
                    c.Item().PaddingTop(26).BorderBottom(1).BorderColor(Dark);
                });
            });

            col.Item().PaddingTop(6).AlignCenter()
                .Text("LT Services sarl — LT Présence")
                .FontSize(7).FontColor(Muted).Italic();
        });
    }

    private static void HCell(IContainer cell, string text, bool alignRight)
    {
        var c = cell.Background(HeaderBg).PaddingVertical(5).PaddingHorizontal(8).AlignMiddle();
        (alignRight ? c.AlignRight() : c).Text(text).FontColor(Colors.White).Bold().FontSize(8.5f);
    }

    private static void BCell(IContainer cell, string text, string bg, bool alignRight)
    {
        var c = cell.Background(bg).BorderBottom(0.5f).BorderColor(Border)
            .PaddingVertical(3.5f).PaddingHorizontal(8).AlignMiddle();
        (alignRight ? c.AlignRight() : c).Text(text).FontSize(8.5f);
    }

    private static byte[]? ChargerLogo()
    {
        try
        {
            foreach (var name in new[]
                     {
                         "Assets/lt_services_brand.jpg",
                         "Assets/lt_services_logo.png",
                         "Assets/lt_services_app.png",
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
            // fallback
        }

        try
        {
            foreach (var name in new[]
                     {
                         "lt_services_brand.jpg",
                         "lt_services_logo.png",
                         "lt_services_app.png",
                         "lt_services_icon.png"
                     })
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

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
