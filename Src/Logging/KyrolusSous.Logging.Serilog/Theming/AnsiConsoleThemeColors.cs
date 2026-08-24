namespace KyrolusSous.Logging.Serilog.Theming;

/// <summary>
/// Modern, high-contrast ANSI theme color palettes for terminal log rendering.
/// </summary>
public static class AnsiConsoleThemeColors
{
    private const string WhiteText = "\u001b[37m";

    /// <summary>
    /// Legacy Visual Studio Mac Light theme.
    /// </summary>
    public static CustomAnsiConsoleTheme VisualStudioMacLight { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = WhiteText,
        [ConsoleThemeStyle.SecondaryText] = WhiteText,
        [ConsoleThemeStyle.TertiaryText] = "\u001b[30;1m",
        [ConsoleThemeStyle.Invalid] = WhiteText,
        [ConsoleThemeStyle.Null] = WhiteText,
        [ConsoleThemeStyle.Name] = WhiteText,
        [ConsoleThemeStyle.String] = WhiteText,
        [ConsoleThemeStyle.Number] = WhiteText,
        [ConsoleThemeStyle.Boolean] = WhiteText,
        [ConsoleThemeStyle.Scalar] = WhiteText,
        [ConsoleThemeStyle.LevelVerbose] = WhiteText,
        [ConsoleThemeStyle.LevelDebug] = "\u001b[44;1m\u001b[37;1m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[42;1m\u001b[37;1m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[43;1m\u001b[37;1m",
        [ConsoleThemeStyle.LevelError] = "\u001b[41;1m\u001b[37;1m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[46;1m\u001b[37;1m",
    });

    /// <summary>
    /// Dracula Dark theme (vibrant Purple, Cyan, Green, Pink).
    /// </summary>
    public static CustomAnsiConsoleTheme Dracula { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;248;248;242m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;98;114;164m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;68;71;90m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;255;85;85m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;189;147;249m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;139;233;253m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;241;250;140m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;189;147;249m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;255;121;198m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;80;250;123m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;98;114;164m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;189;147;249m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;80;250;123m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;255;184;108m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;255;85;85m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;255;85;85m\u001b[38;2;248;248;242;1m",
    });

    /// <summary>
    /// Nord Arctic theme (elegant Frost, Snow Storm, and Polar Night).
    /// </summary>
    public static CustomAnsiConsoleTheme Nord { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;236;239;244m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;143;188;187m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;76;86;106m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;191;97;106m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;180;142;173m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;136;192;208m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;163;190;140m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;180;142;173m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;129;161;193m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;163;190;140m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;76;86;106m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;129;161;193m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;163;190;140m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;235;203;139m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;191;97;106m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;191;97;106m\u001b[38;2;236;239;244;1m",
    });

    /// <summary>
    /// Atom / VS Code One Dark theme.
    /// </summary>
    public static CustomAnsiConsoleTheme OneDark { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;171;178;191m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;92;99;112m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;75;82;99m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;224;108;117m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;209;154;102m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;97;175;239m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;152;195;121m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;209;154;102m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;229;192;123m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;152;195;121m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;92;99;112m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;97;175;239m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;152;195;121m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;229;192;123m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;224;108;117m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;224;108;117m\u001b[38;2;255;255;255;1m",
    });

    /// <summary>
    /// Cyberpunk / Synthwave high-contrast neon palette.
    /// </summary>
    public static CustomAnsiConsoleTheme Cyberpunk { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;0;255;255m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;255;0;127m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;85;85;120m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;255;0;85m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;255;0;255m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;0;255;255m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;57;255;20m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;255;215;0m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;255;0;255m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;57;255;20m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;128;128;128m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;0;255;255m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;57;255;20m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;255;215;0m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;255;0;85m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;255;0;128m\u001b[38;2;255;255;255;1m",
    });

    /// <summary>
    /// Monokai Pro vibrant pastel aesthetic.
    /// </summary>
    public static CustomAnsiConsoleTheme MonokaiPro { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;252;252;250m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;114;112;114m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;85;84;85m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;255;97;136m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;171;157;242m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;120;220;232m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;169;220;118m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;171;157;242m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;255;97;136m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;169;220;118m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;114;112;114m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;120;220;232m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;169;220;118m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;255;216;102m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;255;97;136m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;255;97;136m\u001b[38;2;252;252;250;1m",
    });

    /// <summary>
    /// GitHub Dark theme.
    /// </summary>
    public static CustomAnsiConsoleTheme GitHubDark { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;230;237;243m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;125;133;144m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;48;54;61m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;255;123;114m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;121;192;255m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;210;168;255m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;165;214;255m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;121;192;255m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;255;123;114m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;86;211;100m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;125;133;144m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;121;192;255m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;86;211;100m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;227;179;65m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;255;123;114m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;248;81;73m\u001b[38;2;255;255;255;1m",
    });

    /// <summary>
    /// Solarized Dark theme.
    /// </summary>
    public static CustomAnsiConsoleTheme SolarizedDark { get; } = new(new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\u001b[38;2;131;148;150m",
        [ConsoleThemeStyle.SecondaryText] = "\u001b[38;2;101;123;131m",
        [ConsoleThemeStyle.TertiaryText] = "\u001b[38;2;88;110;117m",
        [ConsoleThemeStyle.Invalid] = "\u001b[38;2;220;50;47m",
        [ConsoleThemeStyle.Null] = "\u001b[38;2;211;54;130m",
        [ConsoleThemeStyle.Name] = "\u001b[38;2;38;139;210m",
        [ConsoleThemeStyle.String] = "\u001b[38;2;42;161;152m",
        [ConsoleThemeStyle.Number] = "\u001b[38;2;211;54;130m",
        [ConsoleThemeStyle.Boolean] = "\u001b[38;2;108;113;196m",
        [ConsoleThemeStyle.Scalar] = "\u001b[38;2;133;153;0m",
        [ConsoleThemeStyle.LevelVerbose] = "\u001b[38;2;101;123;131m",
        [ConsoleThemeStyle.LevelDebug] = "\u001b[38;2;38;139;210m",
        [ConsoleThemeStyle.LevelInformation] = "\u001b[38;2;133;153;0m",
        [ConsoleThemeStyle.LevelWarning] = "\u001b[38;2;181;137;0m",
        [ConsoleThemeStyle.LevelError] = "\u001b[38;2;220;50;47m",
        [ConsoleThemeStyle.LevelFatal] = "\u001b[48;2;220;50;47m\u001b[38;2;253;246;227;1m",
    });
}
