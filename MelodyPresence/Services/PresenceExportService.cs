using ClosedXML.Excel;
using MelodyPresence.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MelodyPresence.Services;

public static class PresenceExportService
{
    static PresenceExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void ExporterExcelResumeMois(
        string chemin,
        string entreprise,
        int annee,
        int mois,
        IReadOnlyList<(Employe Employe, int JoursPresents, double HeuresTotales, int Absences)> lignes)
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
        ws.Range(4, 1, 4, 5).Style.Font.Bold = true;

        var row = 5;
        foreach (var (emp, jours, heures, abs) in lignes)
        {
            ws.Cell(row, 1).Value = emp.Matricule;
            ws.Cell(row, 2).Value = emp.NomComplet;
            ws.Cell(row, 3).Value = jours;
            ws.Cell(row, 4).Value = heures;
            ws.Cell(row, 5).Value = abs;
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
        ws.Range(4, 1, 4, 6).Style.Font.Bold = true;

        var row = 5;
        foreach (var l in lignes)
        {
            ws.Cell(row, 1).Value = l.Matricule;
            ws.Cell(row, 2).Value = l.NomComplet;
            ws.Cell(row, 3).Value = l.PremiereEntree?.ToString("HH:mm") ?? "";
            ws.Cell(row, 4).Value = l.DerniereSortie?.ToString("HH:mm") ?? "";
            ws.Cell(row, 5).Value = l.Heures;
            ws.Cell(row, 6).Value = l.Statut;
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(chemin);
    }

    public static void ExporterPdfResumeMois(
        string chemin,
        string entreprise,
        int annee,
        int mois,
        IReadOnlyList<(Employe Employe, int JoursPresents, double HeuresTotales, int Absences)> lignes)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Column(col =>
                {
                    col.Item().Text(entreprise).Bold().FontSize(16);
                    col.Item().Text($"Résumé présence {mois:D2}/{annee}").FontSize(12);
                });
                page.Content().PaddingTop(20).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Matricule").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Employé").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Présents").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Heures").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Absences").Bold();
                    });
                    foreach (var (emp, jours, heures, abs) in lignes)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(emp.Matricule);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(emp.NomComplet);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(jours.ToString());
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(heures.ToString("0.##"));
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(abs.ToString());
                    }
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Melody Présence — ").FontSize(9);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        }).GeneratePdf(chemin);
    }

    public static void ExporterPdfDetailJour(
        string chemin,
        string entreprise,
        DateTime date,
        IReadOnlyList<JourPresenceLigne> lignes)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Column(col =>
                {
                    col.Item().Text(entreprise).Bold().FontSize(16);
                    col.Item().Text($"Présence du {date:dd/MM/yyyy}").FontSize(12);
                });
                page.Content().PaddingTop(20).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Matricule").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Employé").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Entrée").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Sortie").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Heures").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Statut").Bold();
                    });
                    foreach (var l in lignes)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(l.Matricule);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(l.NomComplet);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .Text(l.PremiereEntree?.ToString("HH:mm") ?? "—");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .Text(l.DerniereSortie?.ToString("HH:mm") ?? "—");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .Text(l.Heures.ToString("0.##"));
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(l.Statut);
                    }
                });
            });
        }).GeneratePdf(chemin);
    }
}
