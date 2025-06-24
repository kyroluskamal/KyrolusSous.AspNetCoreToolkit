namespace KyrolusSous.Logging.Theming;

public static class AnsiConsoleThemeColors
{
    private const string WhiteText = "\u001b[37m";

    public static CustomAnsiConsoleTheme VisualStudioMacLight
    {
        get;
    } = new CustomAnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
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
}
