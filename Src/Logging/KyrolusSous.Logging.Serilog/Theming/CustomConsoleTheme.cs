global using Serilog.Sinks.SystemConsole.Themes;

namespace KyrolusSous.Logging.Serilog.Theming;

/// <summary>
/// Custom console theme with static access to modern themes.
/// </summary>
public class CustomConsoleTheme : ConsoleTheme
{
    private readonly IReadOnlyDictionary<ConsoleThemeStyle, string> _styles;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomConsoleTheme"/> class.
    /// </summary>
    public CustomConsoleTheme(IReadOnlyDictionary<ConsoleThemeStyle, string> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        _styles = styles.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static CustomConsoleTheme Dracula => CustomConsoleThemeColors.Dracula;
    public static CustomConsoleTheme Nord => CustomConsoleThemeColors.Nord;
    public static CustomConsoleTheme OneDark => CustomConsoleThemeColors.OneDark;
    public static CustomConsoleTheme Cyberpunk => CustomConsoleThemeColors.Cyberpunk;
    public static CustomConsoleTheme MonokaiPro => CustomConsoleThemeColors.MonokaiPro;
    public static CustomConsoleTheme GitHubDark => CustomConsoleThemeColors.GitHubDark;
    public static CustomConsoleTheme SolarizedDark => CustomConsoleThemeColors.SolarizedDark;
    public static CustomConsoleTheme VisualStudioMacLight => CustomConsoleThemeColors.VisualStudioMacLight;

    /// <inheritdoc/>
    public override bool CanBuffer => true;

    /// <inheritdoc/>
    protected override int ResetCharCount { get; } = "\u001b[0m".Length;

    /// <inheritdoc/>
    public override int Set(TextWriter output, ConsoleThemeStyle style)
    {
        if (!_styles.TryGetValue(style, out var str))
        {
            return 0;
        }

        output.Write(str);
        return str.Length;
    }

    /// <inheritdoc/>
    public override void Reset(TextWriter output)
    {
        output.Write("\u001b[0m");
    }
}
