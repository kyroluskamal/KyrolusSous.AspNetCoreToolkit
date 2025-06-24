namespace KyrolusSous.Logging.Theming;

public class CustomAnsiConsoleTheme : AnsiConsoleTheme
{
    private readonly IReadOnlyDictionary<ConsoleThemeStyle, string> _styles;

    public CustomAnsiConsoleTheme(IReadOnlyDictionary<ConsoleThemeStyle, string> styles)
        : base(styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        this._styles = styles.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    // A sample of a predefined theme with customized colors
    public readonly static CustomAnsiConsoleTheme VisualStudioMacLight = AnsiConsoleThemeColors.VisualStudioMacLight;

    // Override the Set method if you want to add or modify behavior
    public override int Set(TextWriter output, ConsoleThemeStyle style)
    {
        // Custom logic if necessary, otherwise, call base implementation
        if (!_styles.TryGetValue(style, out var ansiCode))
            return base.Set(output, style); // fall back to base

        output.Write(ansiCode);
        return ansiCode.Length;
    }

    // Reset to default
    public override void Reset(TextWriter output)
    {
        output.Write("\x1b[0m");
    }
}
