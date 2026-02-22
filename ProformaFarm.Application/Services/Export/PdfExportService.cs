using System;
using System.Collections.Generic;
using System.Linq;
using ProformaFarm.Application.Interfaces.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProformaFarm.Application.Services.Export;

public sealed class PdfExportService : IPdfExportService
{
    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] BuildSimpleReport(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        if (headers.Count == 0)
            throw new ArgumentException("PDF report headers must not be empty.", nameof(headers));

        var generatedAtUtc = DateTime.UtcNow;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).Bold().FontSize(14);
                    col.Item().Text($"Gerado em UTC: {generatedAtUtc:O}").FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Element(e => BuildTable(e, headers, rows));

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Pagina ").FontSize(8).FontColor(Colors.Grey.Darken2);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken2);
                    x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken2);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildTable(
        IContainer container,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in headers)
                    columns.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var h in headers)
                {
                    header.Cell()
                        .Background(Colors.Grey.Lighten3)
                        .BorderBottom(1)
                        .BorderColor(Colors.Grey.Lighten1)
                        .PaddingVertical(4)
                        .PaddingHorizontal(6)
                        .Text(h)
                        .Bold()
                        .FontSize(8);
                }
            });

            foreach (var row in rows)
            {
                var safeRow = row;
                if (safeRow.Count < headers.Count)
                {
                    var padded = new string[headers.Count];
                    for (var i = 0; i < headers.Count; i++)
                        padded[i] = i < safeRow.Count ? safeRow[i] ?? string.Empty : string.Empty;
                    safeRow = padded;
                }

                for (var i = 0; i < headers.Count; i++)
                {
                    table.Cell()
                        .BorderBottom(0.5f)
                        .BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(3)
                        .PaddingHorizontal(6)
                        .Text(safeRow[i] ?? string.Empty)
                        .FontSize(8);
                }
            }
        });
    }
}
