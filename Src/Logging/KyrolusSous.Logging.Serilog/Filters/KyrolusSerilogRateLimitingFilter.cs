using KyrolusSous.Logging.Core.Filters;
using Serilog.Core;
using Serilog.Events;

namespace KyrolusSous.Logging.Serilog.Filters;

/// <summary>
/// Serilog event filter that limits identical log message storms.
/// </summary>
public sealed class KyrolusSerilogRateLimitingFilter : ILogEventFilter
{
    private readonly KyrolusLogRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusSerilogRateLimitingFilter"/> class.
    /// </summary>
    /// <param name="rateLimiter">The underlying rate limiter engine.</param>
    public KyrolusSerilogRateLimitingFilter(KyrolusLogRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            return false;
        }

        var key = $"{logEvent.Level}:{logEvent.MessageTemplate.Text}";
        var decision = _rateLimiter.Check(key);

        if (decision.ShouldLog)
        {
            if (decision.SuppressedCount > 0)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty("SuppressedEventsCount", new ScalarValue(decision.SuppressedCount)));
            }

            return true;
        }

        return false;
    }
}
