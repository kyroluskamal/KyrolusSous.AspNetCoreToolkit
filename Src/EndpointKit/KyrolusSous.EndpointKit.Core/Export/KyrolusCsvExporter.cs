using System.Globalization;
using System.Reflection;
using System.Text;

namespace KyrolusSous.EndpointKit.Core.Export;

/// <summary>
/// High-performance, streaming CSV exporter for EndpointKit models and entities.
/// </summary>
public static class KyrolusCsvExporter
{
    public static byte[] ExportToCsv<T>(IEnumerable<T> items, IReadOnlyCollection<string>? selectedFields = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            WriteToStream(items, writer, selectedFields);
        }
        return stream.ToArray();
    }

    public static void WriteToStream<T>(IEnumerable<T> items, TextWriter writer, IReadOnlyCollection<string>? selectedFields = null)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Where(p => selectedFields is null || selectedFields.Count == 0 || selectedFields.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        // Write Header
        for (var i = 0; i < properties.Length; i++)
        {
            if (i > 0) writer.Write(",");
            writer.Write(EscapeCsv(properties[i].Name));
        }
        writer.WriteLine();

        // Write Rows
        foreach (var item in items)
        {
            if (item is null) continue;
            for (var i = 0; i < properties.Length; i++)
            {
                if (i > 0) writer.Write(",");
                var val = properties[i].GetValue(item);
                writer.Write(FormatValue(val));
            }
            writer.WriteLine();
        }
        writer.Flush();
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return string.Empty;
        if (value is bool b) return b ? "true" : "false";
        if (value is DateTime dt) return EscapeCsv(dt.ToString("o", CultureInfo.InvariantCulture));
        if (value is DateTimeOffset dto) return EscapeCsv(dto.ToString("o", CultureInfo.InvariantCulture));
        if (value is DateOnly d) return EscapeCsv(d.ToString("O", CultureInfo.InvariantCulture));
        if (value is IFormattable formattable) return EscapeCsv(formattable.ToString(null, CultureInfo.InvariantCulture));
        return EscapeCsv(value.ToString() ?? string.Empty);
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        var mustQuote = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        if (!mustQuote) return field;

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
