global using Serilog.Sinks.SystemConsole.Themes;

namespace KyrolusSous.BaseConfig.LoggingService;

public class CustomConsoleTheme : ConsoleTheme
{
    private readonly IReadOnlyDictionary<ConsoleThemeStyle, string> _styles;

    public CustomConsoleTheme(IReadOnlyDictionary<ConsoleThemeStyle, string> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        this._styles = styles.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static CustomConsoleTheme VisualStudioMacLight { get; } = CustomConsoleThemeColors.VisualStudioMacLight;

    public override bool CanBuffer
    {
        get
        {
            return true;
        }
    }

    protected override int ResetCharCount { get; } = "\x001B[0m".Length;

    public override int Set(TextWriter output, ConsoleThemeStyle style)
    {
        if (!this._styles.TryGetValue(style, out string? str))
            return 0;
        output.Write(str);
        return str.Length;
    }

    public override void Reset(TextWriter output)
    {
        output.Write("\x001B[0m");
    }
}
