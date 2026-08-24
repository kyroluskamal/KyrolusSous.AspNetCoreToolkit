using System.Collections.Concurrent;
using System.Diagnostics;

namespace KyrolusSous.Logging.Core.Filters;

/// <summary>
/// Result of evaluating a log event against the rate limiter.
/// </summary>
public readonly struct RateLimitDecision
{
    /// <summary>
    /// Gets a value indicating whether the log event is allowed to be logged.
    /// </summary>
    public bool ShouldLog { get; init; }

    /// <summary>
    /// Gets the count of previously suppressed messages that should be reported with this event, if any.
    /// </summary>
    public int SuppressedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether this event was throttled.
    /// </summary>
    public bool IsThrottled => !ShouldLog;
}

/// <summary>
/// High-performance thread-safe sliding window log rate limiter preventing log storm floods.
/// </summary>
public sealed class KyrolusLogRateLimiter
{
    private const int MaxTrackedKeys = 10000;

    private sealed class RateTracker
    {
        public int Count;
        public int Suppressed;
        public long WindowStartTimestamp;
    }

    private readonly ConcurrentDictionary<string, RateTracker> _trackers = new(StringComparer.Ordinal);
    private readonly int _maxOccurrences;
    private readonly TimeSpan _windowDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusLogRateLimiter"/> class.
    /// </summary>
    /// <param name="maxOccurrencesPerWindow">Maximum times identical template can be logged per window.</param>
    /// <param name="windowDuration">The duration of the sliding window.</param>
    public KyrolusLogRateLimiter(int maxOccurrencesPerWindow = 5, TimeSpan? windowDuration = null)
    {
        _maxOccurrences = maxOccurrencesPerWindow > 0 ? maxOccurrencesPerWindow : 5;
        _windowDuration = windowDuration ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Evaluates whether a log event with the given key/template should be logged or suppressed.
    /// </summary>
    /// <param name="messageKey">Unique key or template for the log event.</param>
    /// <returns>Decision indicating if the event should be logged and how many prior events were suppressed.</returns>
    public RateLimitDecision Check(string messageKey)
    {
        if (string.IsNullOrEmpty(messageKey))
        {
            return new RateLimitDecision { ShouldLog = true, SuppressedCount = 0 };
        }

        if (_trackers.Count > MaxTrackedKeys)
        {
            _trackers.Clear();
        }

        var nowTimestamp = Stopwatch.GetTimestamp();
        var tracker = _trackers.GetOrAdd(messageKey, _ => new RateTracker
        {
            Count = 0,
            Suppressed = 0,
            WindowStartTimestamp = nowTimestamp
        });

        lock (tracker)
        {
            var elapsed = Stopwatch.GetElapsedTime(tracker.WindowStartTimestamp, nowTimestamp);
            if (elapsed > _windowDuration)
            {
                var priorSuppressed = tracker.Suppressed;
                tracker.Count = 1;
                tracker.Suppressed = 0;
                tracker.WindowStartTimestamp = nowTimestamp;

                return new RateLimitDecision
                {
                    ShouldLog = true,
                    SuppressedCount = priorSuppressed
                };
            }

            tracker.Count++;
            if (tracker.Count <= _maxOccurrences)
            {
                return new RateLimitDecision
                {
                    ShouldLog = true,
                    SuppressedCount = 0
                };
            }

            tracker.Suppressed++;
            return new RateLimitDecision
            {
                ShouldLog = false,
                SuppressedCount = tracker.Suppressed
            };
        }
    }

    /// <summary>
    /// Clears all tracked rate counters.
    /// </summary>
    public void Reset()
    {
        _trackers.Clear();
    }
}
