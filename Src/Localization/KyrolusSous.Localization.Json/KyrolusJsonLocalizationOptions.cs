namespace KyrolusSous.Localization.Json;

/// <summary>
/// Options for configuring JSON-based localization and file naming rules.
/// </summary>
public sealed class KyrolusJsonLocalizationOptions
{
    /// <summary>
    /// Path to the directory containing JSON localization files.
    /// </summary>
    public string DirectoryPath { get; set; } = "Localization";

    /// <summary>
    /// File pattern filter (e.g. "validation.*.json", "errors.*.json", or "*.json").
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
    /// Optional default fallback culture name (defaults to invariant).
    /// </summary>
    public string FallbackCulture { get; set; } = string.Empty;
}
