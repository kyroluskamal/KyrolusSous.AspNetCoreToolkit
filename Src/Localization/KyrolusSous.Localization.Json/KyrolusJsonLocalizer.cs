namespace KyrolusSous.Localization.Json;

/// <summary>
/// High-performance JSON localization engine supporting category/prefix file enforcement,
/// BCP-47 strict validation, hierarchical culture fallback, nested JSON keys, and template placeholder
/// interpolation. Optionally hot-reloads translations when <see cref="KyrolusJsonLocalizationOptions.EnableHotReload"/>
/// is set.
/// </summary>
public partial class KyrolusJsonLocalizer : IKyrolusLocalizer, IDisposable
{
    private readonly KyrolusJsonLocalizationOptions _options;
    private readonly FileSystemWatcher? _watcher;

    // Reassigned wholesale on every (re)load rather than mutated in place, and always read through this one
    // field, so a hot-reload swaps in a complete, already-built snapshot atomically - a concurrent lookup never
    // observes a partially-populated dictionary.
    private volatile Dictionary<string, IReadOnlyDictionary<string, string>> _cultureTranslations;

    [GeneratedRegex(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z]{4})?(?:-(?:[a-zA-Z]{2}|[0-9]{3}))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Bcp47CultureRegex();

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    public KyrolusJsonLocalizer(KyrolusJsonLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _cultureTranslations = LoadFiles();

        if (_options.EnableHotReload)
        {
            _watcher = new FileSystemWatcher(_options.DirectoryPath, _options.FilePattern)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnDirectoryChanged;
            _watcher.Created += OnDirectoryChanged;
            _watcher.Deleted += OnDirectoryChanged;
            _watcher.Renamed += OnDirectoryChanged;
        }
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

    /// <summary>
    /// Reloads translations on a hot-reload event, keeping the last-good snapshot if the reload fails. This
    /// runs on the <see cref="FileSystemWatcher"/>'s own background callback - there is no caller to propagate
    /// a failure to, and an exception left to escape here (a locked file mid-save, an invalid BCP-47 tag, a
    /// duplicate key while a translator is mid-edit) would go unhandled on that thread and take the whole
    /// process down, which is a far worse outcome than skipping one reload attempt. Catching broadly is
    /// deliberate for exactly that reason.
    /// </summary>
    private void OnDirectoryChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _cultureTranslations = LoadFiles();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Builds a fresh, complete culture-translations snapshot by scanning and loading every matching file from disk.</summary>
    private Dictionary<string, IReadOnlyDictionary<string, string>> LoadFiles()
    {
        if (!Directory.Exists(_options.DirectoryPath))
            throw new DirectoryNotFoundException($"JSON localization directory not found: {_options.DirectoryPath}");

        var candidateFiles = Directory.GetFiles(_options.DirectoryPath, _options.FilePattern);

        var matchingFiles = new List<string>();
        foreach (var file in candidateFiles)
            if (IsCategoryMatch(file, _options.RequiredCategory))
                matchingFiles.Add(file);

        if (matchingFiles.Count == 0)
        {
            var categoryMsg = string.IsNullOrWhiteSpace(_options.RequiredCategory) ? string.Empty : $" matching category '{_options.RequiredCategory}'";
            throw new FileNotFoundException($"No JSON localization files{categoryMsg} matching pattern '{_options.FilePattern}' were found in '{_options.DirectoryPath}'.");
        }

        var cultureTranslations = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in matchingFiles)
            LoadFile(file, cultureTranslations);

        return cultureTranslations;
    }

    private static bool IsCategoryMatch(string filePath, string? requiredCategory)
    {
        if (string.IsNullOrWhiteSpace(requiredCategory)) return true;

        var fileName = Path.GetFileName(filePath);
        var segments = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(s => string.Equals(s, requiredCategory, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadFile(string filePath, Dictionary<string, IReadOnlyDictionary<string, string>> cultureTranslations)
    {
        var cultureKey = ResolveAndValidateCultureKey(filePath);
        var json = File.ReadAllText(filePath);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException(
                $"Localization file '{Path.GetFileName(filePath)}' must contain a top-level JSON object, not a {document.RootElement.ValueKind}.");

        var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenJson(document.RootElement, string.Empty, filePath, flattened);

        if (flattened.Count == 0) return;

        if (cultureTranslations.TryGetValue(cultureKey, out var existing))
        {
            var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in flattened)
            {
                if (_options.ThrowOnDuplicateKeys && merged.ContainsKey(k))
                    throw new InvalidOperationException(
                        $"Duplicate localization key '{k}' for culture '{(cultureKey.Length == 0 ? "invariant" : cultureKey)}' found in '{Path.GetFileName(filePath)}'.");
                merged[k] = v;
            }
            cultureTranslations[cultureKey] = merged;
        }
        else
        {
            cultureTranslations[cultureKey] = flattened;
        }
    }

    /// <summary>
    /// Recursively flattens a JSON document into dot-separated keys (e.g. <c>{"Errors":{"Required":"..."}}</c>
    /// becomes key <c>"Errors.Required"</c>), so translation files can be authored as nested JSON for
    /// readability instead of one flat object. A plain flat file (every value a string, no nesting) flattens to
    /// exactly itself, so this is fully backward-compatible with existing files.
    /// </summary>
    private static void FlattenJson(JsonElement element, string prefix, string filePath, Dictionary<string, string> destination)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
                    FlattenJson(property.Value, key, filePath, destination);
                }
                break;

            case JsonValueKind.String:
                SetLeaf(destination, prefix, element.GetString() ?? string.Empty, filePath);
                break;

            case JsonValueKind.Number:
                SetLeaf(destination, prefix, element.GetRawText(), filePath);
                break;

            case JsonValueKind.True or JsonValueKind.False:
                SetLeaf(destination, prefix, element.GetBoolean() ? "true" : "false", filePath);
                break;

            // Null/array leaves aren't a supported translation shape and are skipped rather than guessed at.
            default:
                break;
        }
    }

    /// <summary>Sets a flattened leaf, rejecting a literal duplicate key within the same file's own JSON (always a mistake, regardless of <see cref="KyrolusJsonLocalizationOptions.ThrowOnDuplicateKeys"/>).</summary>
    private static void SetLeaf(Dictionary<string, string> destination, string key, string value, string filePath)
    {
        if (!destination.TryAdd(key, value))
            throw new ArgumentException(
                $"Duplicate localization key '{key}' found more than once within '{Path.GetFileName(filePath)}'.", nameof(filePath));
    }

    private string ResolveAndValidateCultureKey(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');
        string? fileCultureTag = null;

        if (parts.Length > 1) fileCultureTag = parts[^1];

        else if (Bcp47CultureRegex().IsMatch(parts[0]))
        {
            // An uncategorized, undotted file (e.g. "messages.json") is only treated as a culture
            // tag when it actually looks like one; otherwise it's an invariant file, not a broken one.
            fileCultureTag = parts[0];
        }

        if (string.IsNullOrWhiteSpace(fileCultureTag) ||
            string.Equals(fileCultureTag, _options.RequiredCategory, StringComparison.OrdinalIgnoreCase))
            return string.Empty; // Invariant fallback

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

        var translations = _cultureTranslations; // snapshot: one consistent view for this whole call
        var targetCulture = culture ?? CultureInfo.CurrentUICulture;

        foreach (var cultureCandidate in KyrolusCultureFallbackResolver.GetCandidates(targetCulture, _options.GetEffectiveFallbackCultures()))
            if (translations.TryGetValue(cultureCandidate, out var dict) &&
                dict.TryGetValue(key, out var translation))
                return new KyrolusLocalizationResult(translation, ResourceNotFound: false, SearchedLocation: cultureCandidate);

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

    /// <inheritdoc />
    public IEnumerable<string> GetAllKeys(CultureInfo? culture = null)
    {
        var translations = _cultureTranslations; // snapshot: one consistent view for this whole call
        var targetCulture = culture ?? CultureInfo.CurrentUICulture;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureCandidate in KyrolusCultureFallbackResolver.GetCandidates(targetCulture, _options.GetEffectiveFallbackCultures()))
            if (translations.TryGetValue(cultureCandidate, out var dict))
                foreach (var key in dict.Keys)
                    keys.Add(key);

        return keys;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableCultures()
        => [.. _cultureTranslations.Keys.Where(k => k.Length > 0)];

    /// <summary>Stops watching the directory (a no-op when <see cref="KyrolusJsonLocalizationOptions.EnableHotReload"/> was never enabled).</summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        GC.SuppressFinalize(this);
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
