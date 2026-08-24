using KyrolusSous.Logging.Serilog.Theming;
using Serilog.Sinks.SystemConsole.Themes;

namespace KyrolusSous.Logging.UnitTests;

public class ThemeTests
{
    [Fact(DisplayName = "CustomConsoleTheme should write mapped style and length")]
    public void CustomConsoleTheme_Should_Write_Mapped_Style()
    {
        var theme = CustomConsoleThemeColors.VisualStudioMacLight;
        using var writer = new StringWriter();

        var expected = "\u001b[41;1m\u001b[37;1m"; // LevelError mapping in CustomConsoleThemeColors
        var len = theme.Set(writer, ConsoleThemeStyle.LevelError);

        writer.ToString().ShouldBe(expected);
        len.ShouldBe(expected.Length);
    }

    [Fact(DisplayName = "CustomConsoleTheme should reset with escape sequence")]
    public void CustomConsoleTheme_Should_Reset_With_Escape_Sequence()
    {
        var theme = CustomConsoleThemeColors.VisualStudioMacLight;
        using var writer = new StringWriter();

        theme.Reset(writer);

        writer.ToString().ShouldBe("\x001B[0m");
        theme.CanBuffer.ShouldBeTrue();
    }

    [Fact(DisplayName = "CustomAnsiConsoleTheme should write configured style")]
    public void CustomAnsiConsoleTheme_Should_Write_Configured_Style()
    {
        var styles = new Dictionary<ConsoleThemeStyle, string>
        {
            [ConsoleThemeStyle.Text] = "\x1b[32m",
            [ConsoleThemeStyle.LevelWarning] = "\x1b[33m"
        };

        var theme = new CustomAnsiConsoleTheme(styles);
        using var writer = new StringWriter();

        var len = theme.Set(writer, ConsoleThemeStyle.LevelWarning);

        writer.ToString().ShouldBe("\x1b[33m");
        len.ShouldBe("\x1b[33m".Length);
    }

    [Fact(DisplayName = "CustomAnsiConsoleTheme should reset with escape sequence")]
    public void CustomAnsiConsoleTheme_Should_Reset_With_Escape_Sequence()
    {
        var theme = CustomAnsiConsoleTheme.VisualStudioMacLight;
        using var writer = new StringWriter();

        theme.Reset(writer);

        writer.ToString().ShouldBe("\x1b[0m");
    }

    [Fact(DisplayName = "CustomConsoleThemeColors should expose all modern themes")]
    public void CustomConsoleThemeColors_Should_Expose_All_Modern_Themes()
    {
        CustomConsoleTheme.Dracula.ShouldNotBeNull();
        CustomConsoleTheme.Nord.ShouldNotBeNull();
        CustomConsoleTheme.OneDark.ShouldNotBeNull();
        CustomConsoleTheme.Cyberpunk.ShouldNotBeNull();
        CustomConsoleTheme.MonokaiPro.ShouldNotBeNull();
        CustomConsoleTheme.GitHubDark.ShouldNotBeNull();
        CustomConsoleTheme.SolarizedDark.ShouldNotBeNull();
        CustomConsoleTheme.VisualStudioMacLight.ShouldNotBeNull();
    }

    [Fact(DisplayName = "CustomAnsiConsoleTheme should expose all modern themes")]
    public void CustomAnsiConsoleTheme_Should_Expose_All_Modern_Themes()
    {
        CustomAnsiConsoleTheme.Dracula.ShouldNotBeNull();
        CustomAnsiConsoleTheme.Nord.ShouldNotBeNull();
        CustomAnsiConsoleTheme.OneDark.ShouldNotBeNull();
        CustomAnsiConsoleTheme.Cyberpunk.ShouldNotBeNull();
        CustomAnsiConsoleTheme.MonokaiPro.ShouldNotBeNull();
        CustomAnsiConsoleTheme.GitHubDark.ShouldNotBeNull();
        CustomAnsiConsoleTheme.SolarizedDark.ShouldNotBeNull();
        CustomAnsiConsoleTheme.VisualStudioMacLight.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Dracula and Nord themes format levels correctly")]
    public void ModernThemes_FormatLevels_Correctly()
    {
        using var draculaWriter = new StringWriter();
        var draculaLen = CustomAnsiConsoleTheme.Dracula.Set(draculaWriter, ConsoleThemeStyle.LevelError);
        draculaLen.ShouldBeGreaterThan(0);
        draculaWriter.ToString().ShouldContain("255;85;85");

        using var nordWriter = new StringWriter();
        var nordLen = CustomAnsiConsoleTheme.Nord.Set(nordWriter, ConsoleThemeStyle.LevelInformation);
        nordLen.ShouldBeGreaterThan(0);
        nordWriter.ToString().ShouldContain("163;190;140");
    }
}
