using System.Collections.Concurrent;

namespace KyrolusSous.Resilience;

/// <summary>
/// In-memory thread-safe implementation of <see cref="IKyrolusResilienceQuarantine"/>.
/// Hardened against unbounded memory growth.
/// </summary>
public class KyrolusResilienceQuarantine : IKyrolusResilienceQuarantine
{
    private const int MaxTrackedQuarantines = 10000;

    private sealed class QuarantineRecord
    {
        public int ConsecutiveFailures;
        public DateTimeOffset? QuarantinedUntilUtc;
    }

    private readonly ConcurrentDictionary<string, QuarantineRecord> _records = new(StringComparer.Ordinal);
    private readonly TimeSpan _defaultQuarantineDuration = TimeSpan.FromMinutes(1);

    public bool IsQuarantined(string requestKey)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) return false;

        if (_records.TryGetValue(requestKey, out var record) && record.QuarantinedUntilUtc.HasValue)
        {
            if (DateTimeOffset.UtcNow < record.QuarantinedUntilUtc.Value)
            {
                return true;
            }

            // Quarantine expired
            record.QuarantinedUntilUtc = null;
            record.ConsecutiveFailures = 0;
        }

        return false;
    }

    public void RecordFailure(string requestKey, int failureThreshold = 3, TimeSpan? quarantineDuration = null)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) return;

        if (_records.Count >= MaxTrackedQuarantines && !_records.ContainsKey(requestKey))
        {
            PruneExpiredRecords();
        }

        var record = _records.GetOrAdd(requestKey, _ => new QuarantineRecord());
        var failures = Interlocked.Increment(ref record.ConsecutiveFailures);

        if (failures >= failureThreshold)
        {
            var duration = quarantineDuration ?? _defaultQuarantineDuration;
            record.QuarantinedUntilUtc = DateTimeOffset.UtcNow.Add(duration);
        }
    }

    public void RecordSuccess(string requestKey)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) return;

        if (_records.TryGetValue(requestKey, out var record))
        {
            Interlocked.Exchange(ref record.ConsecutiveFailures, 0);
            record.QuarantinedUntilUtc = null;
        }
    }

    private void PruneExpiredRecords()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _records)
        {
            if (!kvp.Value.QuarantinedUntilUtc.HasValue || kvp.Value.QuarantinedUntilUtc.Value < now)
            {
                _records.TryRemove(kvp.Key, out _);
            }
        }
    }
}
