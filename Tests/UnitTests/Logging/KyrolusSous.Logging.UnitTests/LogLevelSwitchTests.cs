using KyrolusSous.Logging.Core.LevelSwitch;

namespace KyrolusSous.Logging.UnitTests;

public class LogLevelSwitchTests
{
    [Fact(DisplayName = "LogLevelSwitch: Sets minimum level and invokes notification event")]
    public void LogLevelSwitch_SetMinimumLevel_UpdatesAndNotifies()
    {
        var levelSwitch = new KyrolusLogLevelSwitch(LogLevel.Information);
        levelSwitch.MinimumLevel.ShouldBe(LogLevel.Information);

        LogLevel? observed = null;
        levelSwitch.MinimumLevelChanged += lvl => observed = lvl;

        levelSwitch.SetMinimumLevel(LogLevel.Debug);

        levelSwitch.MinimumLevel.ShouldBe(LogLevel.Debug);
        observed.ShouldBe(LogLevel.Debug);
    }

    [Fact(DisplayName = "LogLevelSwitch: Temporary boost reverts immediately when scope is disposed")]
    public void LogLevelSwitch_BoostLevel_RevertsOnDispose()
    {
        var levelSwitch = new KyrolusLogLevelSwitch(LogLevel.Warning);

        using (levelSwitch.BoostLevel(LogLevel.Trace, TimeSpan.FromMinutes(5)))
        {
            levelSwitch.MinimumLevel.ShouldBe(LogLevel.Trace);
        }

        levelSwitch.MinimumLevel.ShouldBe(LogLevel.Warning);
    }

    [Fact(DisplayName = "LogLevelSwitch: Temporary boost automatically reverts after timer duration")]
    public async Task LogLevelSwitch_BoostLevel_RevertsOnTimer()
    {
        var levelSwitch = new KyrolusLogLevelSwitch(LogLevel.Error);

        var scope = levelSwitch.BoostLevel(LogLevel.Debug, TimeSpan.FromMilliseconds(50));
        levelSwitch.MinimumLevel.ShouldBe(LogLevel.Debug);

        await Task.Delay(100);

        levelSwitch.MinimumLevel.ShouldBe(LogLevel.Error);
        scope.Dispose(); // Safe disposal after auto-revert
    }
}
