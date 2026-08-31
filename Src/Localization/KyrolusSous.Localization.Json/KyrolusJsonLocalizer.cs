using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using KyrolusSous.Localization.Abstractions;

namespace KyrolusSous.Localization.Json;

/// <summary>
/// High-performance JSON localization engine supporting category/prefix file enforcement,
/// BCP-47 strict validation, hierarchical culture fallback, and template placeholder interpolation.
/// </summary>
public partial class KyrolusJsonLocalizer : IKyrolusLocalizer
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cultureTranslations = new(StringComparer.OrdinalIgnoreCase);
    private readonly KyrolusJsonLocalizationOptions _options;

    [GeneratedRegex(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z]{4})?(?:-(?:[a-zA-Z]{2}|[0-9]{3}))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Bcp47CultureRegex();

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    public KyrolusJsonLocalizer(KyrolusJsonLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        LoadFiles();
    }

    /// <summary>
    /// Initializes a new instance pointing to a directory with an optional file pattern and required category.
    /// </summary>
    public KyrolusJsonLocalizer(string directoryPath, string filePattern = "*.json", string? requiredCategory = null)
        : this(new KyrolusJsonLocalizationOptions
        {
            DirectoryPath = directoryPath,
            FilePattern = filePattern,
            RequiredCategory = requiredCategory
        })
    {
    }

    private void LoadFiles()
    {
        if (!Directory.Exists(_options.DirectoryPath))
            throw new DirectoryNotFoundException($"JSON localization directory not found: {_options.DirectoryPath}");

        var candidateFiles = Directory.GetFiles(_options.DirectoryPath, _options.FilePattern);

        var matchingFiles = new List<string>();
        foreach (var file in candidateFiles)
        {
            if (IsCategoryMatch(file, _options.RequiredCategory))
                matchingFiles.Add(file);
        }

        if (matchingFiles.Count == 0)
        {
            var categoryMsg = string.IsNullOrWhiteSpace(_options.RequiredCategory) ? string.Empty : $" matching category '{_options.RequiredCategory}'";
            throw new FileNotFoundException($"No JSON localization files{categoryMsg} matching pattern '{_options.FilePattern}' were found in '{_options.DirectoryPath}'.");
        }

        foreach (var file in matchingFiles)
            LoadFile(file);
    }

    private static bool IsCategoryMatch(string filePath, string? requiredCategory)
    {
        if (string.IsNullOrWhiteSpace(requiredCategory))
            return true;

        var fileName = Path.GetFileName(filePath);
        var segments = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(s => string.Equals(s, requiredCategory, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadFile(string filePath)
    {
        var cultureKey = ResolveAndValidateCultureKey(filePath);
        var json = File.ReadAllText(filePath);
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        if (translations is null) return;

        if (_cultureTranslations.TryGetValue(cultureKey, out var existing))
        {
            var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in translations)
                merged[k] = v;
            _cultureTranslations[cultureKey] = merged;
        }
        else
        {
            _cultureTranslations[cultureKey] = new Dictionary<string, string>(translations, StringComparer.OrdinalIgnoreCase);
        }
    }

    private string ResolveAndValidateCultureKey(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');
        string? fileCultureTag = null;

        if (parts.Length > 1)
        {
            fileCultureTag = parts[^1];
        }
        else if (Bcp47CultureRegex().IsMatch(parts[0]))
        {
            // An uncategorized, undotted file (e.g. "messages.json") is only treated as a culture
            // tag when it actually looks like one; otherwise it's an invariant file, not a broken one.
            fileCultureTag = parts[0];
        }

        if (string.IsNullOrWhiteSpace(fileCultureTag) ||
            string.Equals(fileCultureTag, _options.RequiredCategory, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty; // Invariant fallback
        }

        if (_options.StrictBcp47Validation && !Bcp47CultureRegex().IsMatch(fileCultureTag))
            throw new ArgumentException(
                $"Invalid culture tag '{fileCultureTag}' in localization file '{Path.GetFileName(filePath)}'. Must follow BCP-47 standard format (e.g. 'ar', 'ar-EG', 'en-US').", nameof(filePath));

        return fileCultureTag;
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new KyrolusLocalizationResult(string.Empty, ResourceNotFound: true);

        var targetCulture = culture ?? CultureInfo.CurrentUICulture;

        foreach (var cultureCandidate in GetCultureCandidates(targetCulture))
        {
            if (_cultureTranslations.TryGetValue(cultureCandidate, out var dict) &&
                dict.TryGetValue(key, out var translation))
            {
                return new KyrolusLocalizationResult(translation, ResourceNotFound: false, SearchedLocation: cultureCandidate);
            }
        }

        return new KyrolusLocalizationResult(key, ResourceNotFound: true);
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
    {
        var result = GetString(key, culture);
        if (arguments is null || string.IsNullOrEmpty(result.Value))
            return result;

        var formatted = Format(result.Value, arguments);
        return new KyrolusLocalizationResult(formatted, result.ResourceNotFound, result.SearchedLocation);
    }

    /// <inheritdoc />
    public string Format(string template, object? arguments) => KyrolusLocalizationFormatter.Format(template, arguments);

    private static IEnumerable<string> GetCultureCandidates(CultureInfo culture)
    {
        if (culture is null)
        {
            yield return string.Empty;
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(culture.Name))
            yield return culture.Name;

        var parentName = culture.Parent?.Name;
        if (!string.IsNullOrWhiteSpace(parentName))
            yield return parentName;

        if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName) &&
            !string.Equals(culture.TwoLetterISOLanguageName, culture.Name, StringComparison.OrdinalIgnoreCase))
            yield return culture.TwoLetterISOLanguageName;

        yield return string.Empty;
    }
}

/// <summary>
/// Strongly-typed JSON localizer implementation.
/// </summary>
public sealed class KyrolusJsonLocalizer<TCategory> : KyrolusJsonLocalizer, IKyrolusLocalizer<TCategory>
{
    public KyrolusJsonLocalizer(KyrolusJsonLocalizationOptions options) : base(options)
    {
    }

    public KyrolusJsonLocalizer(string directoryPath, string filePattern = "*.json", string? requiredCategory = null)
        : base(directoryPath, filePattern, requiredCategory ?? typeof(TCategory).Name.ToLowerInvariant())
    {
    }
}
