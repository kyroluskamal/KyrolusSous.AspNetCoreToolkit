namespace KyrolusSous.Logging.Serilog.Theming;

/// <summary>
/// ANSI console theme wrapper with pre-configured modern developer color schemes.
/// </summary>
public class CustomAnsiConsoleTheme : AnsiConsoleTheme
{
    private readonly IReadOnlyDictionary<ConsoleThemeStyle, string> _styles;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAnsiConsoleTheme"/> class.
    /// </summary>
    public CustomAnsiConsoleTheme(IReadOnlyDictionary<ConsoleThemeStyle, string> styles)
        : base(styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        _styles = styles.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Dracula dark theme (Purple, Cyan, Green, Pink, Orange).
    /// </summary>
    public static CustomAnsiConsoleTheme Dracula => AnsiConsoleThemeColors.Dracula;

    /// <summary>
    /// Nord arctic theme (Frost, Snow Storm, and Polar Night).
    /// </summary>
    public static CustomAnsiConsoleTheme Nord => AnsiConsoleThemeColors.Nord;

    /// <summary>
    /// Atom / VS Code One Dark theme.
    /// </summary>
    public static CustomAnsiConsoleTheme OneDark => AnsiConsoleThemeColors.OneDark;

    /// <summary>
    /// Cyberpunk / Synthwave high-contrast neon theme.
    /// </summary>
    public static CustomAnsiConsoleTheme Cyberpunk => AnsiConsoleThemeColors.Cyberpunk;

    /// <summary>
    /// Monokai Pro vibrant pastel theme.
    /// </summary>
    public static CustomAnsiConsoleTheme MonokaiPro => AnsiConsoleThemeColors.MonokaiPro;

    /// <summary>
    /// GitHub Dark theme.
    /// </summary>
    public static CustomAnsiConsoleTheme GitHubDark => AnsiConsoleThemeColors.GitHubDark;

    /// <summary>
    /// Solarized Dark theme.
    /// </summary>
    public static CustomAnsiConsoleTheme SolarizedDark => AnsiConsoleThemeColors.SolarizedDark;

    /// <summary>
    /// Legacy Visual Studio Mac Light theme.
    /// </summary>
    public static CustomAnsiConsoleTheme VisualStudioMacLight => AnsiConsoleThemeColors.VisualStudioMacLight;

    /// <inheritdoc/>
    public override int Set(TextWriter output, ConsoleThemeStyle style)
    {
        if (!_styles.TryGetValue(style, out var ansiCode))
        {
            return base.Set(output, style);
        }

        output.Write(ansiCode);
        return ansiCode.Length;
    }

    /// <inheritdoc/>
    public override void Reset(TextWriter output)
    {
        output.Write("\u001b[0m");
    }
}
