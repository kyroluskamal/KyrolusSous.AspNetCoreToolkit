using System.Collections.Concurrent;

namespace KyrolusSous.Auth.Security;

/// <summary>
/// Service contract for protecting authentication endpoints against brute-force and credential-stuffing attacks.
/// </summary>
public interface IKyrolusBruteForceGuard
{
    ValueTask<bool> IsLockedOutAsync(string key, CancellationToken cancellationToken = default);
    ValueTask RecordFailedAttemptAsync(string key, CancellationToken cancellationToken = default);
    ValueTask ResetAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for configuring brute-force lockout rules.
/// </summary>
public sealed class KyrolusBruteForceOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// High-performance in-memory implementation of <see cref="IKyrolusBruteForceGuard"/>.
/// </summary>
public sealed class KyrolusInMemoryBruteForceGuard : IKyrolusBruteForceGuard
{
    private sealed record AttemptRecord(int FailedCount, DateTimeOffset? LockoutUntil, DateTimeOffset LastFailedAt);

    private readonly ConcurrentDictionary<string, AttemptRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly KyrolusBruteForceOptions _options;

    public KyrolusInMemoryBruteForceGuard(KyrolusBruteForceOptions? options = null)
    {
        _options = options ?? new KyrolusBruteForceOptions();
    }

    public ValueTask<bool> IsLockedOutAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var cleanKey = key.Trim();
        if (_records.TryGetValue(cleanKey, out var record) && record.LockoutUntil.HasValue)
        {
            if (DateTimeOffset.UtcNow < record.LockoutUntil.Value)
            {
                return ValueTask.FromResult(true);
            }

            // Lockout has expired, reset
            _records.TryRemove(cleanKey, out _);
        }

        return ValueTask.FromResult(false);
    }

    public ValueTask RecordFailedAttemptAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var cleanKey = key.Trim();
        var now = DateTimeOffset.UtcNow;

        _records.AddOrUpdate(
            cleanKey,
            _ => new AttemptRecord(1, null, now),
            (_, existing) =>
            {
                // If previous lockout expired, reset counter to 1
                if (existing.LockoutUntil.HasValue && now >= existing.LockoutUntil.Value)
                {
                    return new AttemptRecord(1, null, now);
                }

                var newCount = existing.FailedCount + 1;
                if (newCount >= _options.MaxFailedAttempts)
                {
                    return new AttemptRecord(newCount, now.Add(_options.LockoutDuration), now);
                }

                return new AttemptRecord(newCount, null, now);
            });

        if (_records.Count > 5000)
        {
            PurgeExpiredRecords();
        }

        return ValueTask.CompletedTask;
    }

    public void PurgeExpiredRecords()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (k, v) in _records)
        {
            if ((v.LockoutUntil.HasValue && now >= v.LockoutUntil.Value) ||
                (!v.LockoutUntil.HasValue && now - v.LastFailedAt > _options.LockoutDuration))
            {
                _records.TryRemove(k, out _);
            }
        }
    }

    public ValueTask ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _records.TryRemove(key.Trim(), out _);
        return ValueTask.CompletedTask;
    }
}
