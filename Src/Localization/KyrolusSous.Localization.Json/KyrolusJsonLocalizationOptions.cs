namespace KyrolusSous.Localization.Json;

/// <summary>
/// Options for configuring JSON-based localization and file naming rules.
/// </summary>
public sealed class KyrolusJsonLocalizationOptions
{
    /// <summary>
    /// Path to the directory containing JSON localization files. This is a trusted, developer-configured
    /// setting read directly with <see cref="System.IO.Directory.GetFiles(string, string)"/> - never build it
    /// from unsanitized request input (e.g. a tenant id taken straight from a URL), since that opens a path
    /// traversal risk (a segment like <c>"..\.."</c> could point the scan outside the intended directory).
    /// </summary>
    public string DirectoryPath { get; set; } = "Localization";

    /// <summary>
    /// File pattern filter (e.g. "validation.*.json", "errors.*.json", or "*.json"). Same trust boundary as
    /// <see cref="DirectoryPath"/>: this is a glob passed straight to <see cref="System.IO.Directory.GetFiles(string, string)"/>,
    /// not something to build from request input.
    /// </summary>
    public string FilePattern { get; set; } = "*.json";

    /// <summary>
    /// Optional required category or prefix (e.g. "validation", "errors").
    /// When set, only files matching this prefix/category segment will be loaded and other files in the same folder are excluded.
    /// </summary>
    public string? RequiredCategory { get; set; }

    /// <summary>
    /// Indicates whether to strictly validate BCP-47 culture tags in file names at startup (fail-fast).
    /// </summary>
    public bool StrictBcp47Validation { get; set; } = true;

    /// <summary>
    /// Optional culture name (e.g. "en") to try - after the requested culture's own hierarchy is exhausted -
    /// before giving up to the invariant bucket. Defaults to none, in which case only the requested culture's
    /// own hierarchy and the invariant bucket are tried. An unrecognized culture name is ignored rather than
    /// throwing.
    /// </summary>
    public string FallbackCulture { get; set; } = string.Empty;

    /// <summary>
    /// Additional culture names to try, in order, after <see cref="FallbackCulture"/> and before giving up to
    /// the invariant bucket - for apps that need more than one fallback level (e.g. a regional dialect, then a
    /// secondary administrative language, then finally invariant). Each name's own culture hierarchy is tried
    /// in turn. Defaults to none.
    /// </summary>
    public IReadOnlyList<string> FallbackCultures { get; set; } = [];

    /// <summary>The effective, ordered fallback-culture chain: <see cref="FallbackCulture"/> (if set) followed by <see cref="FallbackCultures"/>.</summary>
    internal IEnumerable<string> GetEffectiveFallbackCultures()
    {
        if (!string.IsNullOrWhiteSpace(FallbackCulture))
            yield return FallbackCulture;

        foreach (var name in FallbackCultures)
            yield return name;
    }

    /// <summary>
    /// When <see langword="true"/>, watches <see cref="DirectoryPath"/> for file changes and reloads
    /// translations automatically (a changed/created/deleted/renamed file matching <see cref="FilePattern"/>
    /// triggers a full reload). Off by default: most apps load translations once at startup and don't need the
    /// extra <see cref="System.IO.FileSystemWatcher"/> handle. Reloads swap in a freshly-loaded, immutable
    /// snapshot atomically, so concurrent lookups never observe a partially-loaded state; a reload that fails
    /// (e.g. a file is transiently locked mid-save) is skipped, keeping the last-good translations in place.
    /// </summary>
    public bool EnableHotReload { get; set; } = false;

    /// <summary>
    /// When <see langword="true"/>, loading two files that define the same key for the same culture throws at
    /// startup (or on the next hot-reload) instead of silently letting the later file's value win. Off by
    /// default, since intentionally layering an "overrides" file on top of a "defaults" file for the same
    /// culture is a common, valid pattern. A duplicate key within the same file's own JSON object is always
    /// rejected regardless of this setting, since that is never intentional.
    /// </summary>
    public bool ThrowOnDuplicateKeys { get; set; } = false;
}
