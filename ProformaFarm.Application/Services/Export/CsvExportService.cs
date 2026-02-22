using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProformaFarm.Application.Interfaces.Export;

namespace ProformaFarm.Application.Services.Export;

public sealed class CsvExportService : ICsvExportService
{
    public string BuildCsv<T>(
        IReadOnlyList<T> rows,
        IReadOnlyList<(string Header, Func<T, object?> ValueSelector)> columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Count == 0)
            throw new ArgumentException("CSV columns must not be empty.", nameof(columns));

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => EscapeCsv(c.Header))));

        foreach (var row in rows)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');

                var raw = columns[i].ValueSelector(row);
                sb.Append(EscapeCsv(ConvertToString(raw)));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ConvertToString(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is string text)
            return text;

        if (value is DateTime dateTime)
            return dateTime.ToString("O", CultureInfo.InvariantCulture);

        if (value is DateTimeOffset dateTimeOffset)
            return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
