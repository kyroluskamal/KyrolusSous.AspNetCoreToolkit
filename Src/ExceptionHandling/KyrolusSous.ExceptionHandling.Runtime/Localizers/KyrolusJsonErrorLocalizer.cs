using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

namespace KyrolusSous.ExceptionHandling.Runtime.Localizers;

/// <summary>
/// Loads and manages JSON-based error translations from a directory with strict BCP-47 culture validation
/// and hierarchical culture fallback (e.g. ar-EG -> ar -> invariant).
/// </summary>
public sealed partial class KyrolusJsonErrorLocalizer : IKyrolusErrorLocalizer
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cultureTranslations = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    [GeneratedRegex(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z]{4})?(?:-(?:[a-zA-Z]{2}|[0-9]{3}))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Bcp47CultureRegex();

    /// <summary>
    /// Initializes a new instance by scanning a directory for JSON translation files.
    /// Validates all file names strictly against BCP-47 standards at startup (fail-fast).
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing JSON translation files.</param>
    /// <param name="searchPattern">File search pattern when scanning the directory (default: "*.json").</param>
    public KyrolusJsonErrorLocalizer(string directoryPath, string searchPattern = "*.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"JSON localization directory not found: {directoryPath}");

        var files = Directory.GetFiles(directoryPath, searchPattern);
        if (files.Length == 0)
            throw new FileNotFoundException($"No JSON localization files matching '{searchPattern}' were found in directory '{directoryPath}'.");

        foreach (var file in files)            LoadFile(file);
    }

    private void LoadFile(string filePath)
    {
        var cultureKey = ResolveAndValidateCultureKey(filePath);

        var json = File.ReadAllText(filePath);
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);

        if (translations is not null)
        {
            if (_cultureTranslations.TryGetValue(cultureKey, out var existing))
            {
                // Merge if multiple files map to the same culture
                var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in translations)
                {
                    merged[k] = v;
                }
                _cultureTranslations[cultureKey] = merged;
            }
            else
            {
                _cultureTranslations[cultureKey] = new Dictionary<string, string>(translations, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <inheritdoc />
    public string? Localize(string code, string? defaultMessage, CultureInfo? culture)
    {
        if (string.IsNullOrWhiteSpace(code))            return defaultMessage;

        if (culture is not null && !string.IsNullOrWhiteSpace(culture.Name))
        {
            // 1. Try exact culture match (e.g. "ar-EG")
            if (_cultureTranslations.TryGetValue(culture.Name, out var exactDict) &&
                exactDict.TryGetValue(code, out var exactValue))
                return exactValue;

            // 2. Try parent culture fallback (e.g. "ar-EG" -> "ar")
            var parent = culture.Parent;
            if (parent is not null && !string.IsNullOrWhiteSpace(parent.Name))
                if (_cultureTranslations.TryGetValue(parent.Name, out var parentDict) &&
                    parentDict.TryGetValue(code, out var parentValue))
                    return parentValue;

            // 3. Try two-letter ISO language name fallback (e.g. "ar")
            if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName) &&
                !string.Equals(culture.TwoLetterISOLanguageName, culture.Name, StringComparison.OrdinalIgnoreCase))
                if (_cultureTranslations.TryGetValue(culture.TwoLetterISOLanguageName, out var isoDict) &&
                    isoDict.TryGetValue(code, out var isoValue))
                    return isoValue;
        }

        // 4. Try default / invariant culture (key: "")
        if (_cultureTranslations.TryGetValue(string.Empty, out var defaultDict) &&
            defaultDict.TryGetValue(code, out var defaultValue))
            return defaultValue;

        // 5. Fallback to default message
        return defaultMessage;
    }

    private static string ResolveAndValidateCultureKey(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');
        string? fileCultureTag = null;

        if (parts.Length > 1)
            fileCultureTag = parts[^1];
        else if (Bcp47CultureRegex().IsMatch(parts[0]))
            fileCultureTag = parts[0];

        if (string.IsNullOrWhiteSpace(fileCultureTag))
            return string.Empty; // Invariant default (e.g. "errors.json" or "translations.json")

        if (!Bcp47CultureRegex().IsMatch(fileCultureTag))
            throw new ArgumentException(
                $"Invalid culture tag '{fileCultureTag}' in file '{Path.GetFileName(filePath)}'. " +
                "Must follow BCP-47 standard format (e.g. 'ar', 'ar-EG', 'en-US').", nameof(filePath));

        return fileCultureTag;
    }
}
