using KyrolusSous.Logging.Theming;
using Serilog.Sinks.SystemConsole.Themes;

namespace KyrolusSous.Logging.Tests;

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

    [Fact(DisplayName = "CustomConsoleThemeColors should expose predefined theme")]
    public void CustomConsoleThemeColors_Should_Expose_Predefined_Theme()
    {
        var theme = CustomConsoleThemeColors.VisualStudioMacLight;
        using var writer = new StringWriter();

        var len = theme.Set(writer, ConsoleThemeStyle.LevelInformation);

        writer.ToString().ShouldBe("\u001b[42;1m\u001b[37;1m");
        len.ShouldBe("\u001b[42;1m\u001b[37;1m".Length);
    }
}
