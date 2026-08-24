namespace KyrolusSous.Logging.Abstractions.LevelSwitch;

/// <summary>
/// Controls the minimum log level at runtime dynamically without restarting the application.
/// </summary>
public interface IKyrolusLogLevelSwitch
{
    /// <summary>
    /// Gets the current minimum log level.
    /// </summary>
    LogLevel MinimumLevel { get; }

    /// <summary>
    /// Updates the minimum log level immediately.
    /// </summary>
    /// <param name="newLevel">The new minimum log level.</param>
    void SetMinimumLevel(LogLevel newLevel);

    /// <summary>
    /// Temporarily boosts the log level for a specific duration, after which it automatically reverts to the previous level.
    /// </summary>
    /// <param name="boostLevel">The temporary log level.</param>
    /// <param name="duration">The duration to keep the boosted level active.</param>
    /// <returns>An <see cref="IDisposable"/> that reverts the boost immediately when disposed.</returns>
    IDisposable BoostLevel(LogLevel boostLevel, TimeSpan duration);
}
