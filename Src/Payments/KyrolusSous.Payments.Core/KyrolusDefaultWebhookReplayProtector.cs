using System.Collections.Concurrent;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultWebhookReplayProtector(IKyrolusCacheProvider? cacheProvider = null) : IKyrolusWebhookReplayProtector
{
    private static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seenEvents = new();

    public async Task<bool> ValidateAndRecordWebhookAsync(
        string eventId,
        DateTimeOffset eventTimestampUtc,
        TimeSpan? toleranceWindow = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return false;

        var tolerance = toleranceWindow ?? DefaultTolerance;
        var age = DateTimeOffset.UtcNow - eventTimestampUtc;

        // Check if event is too old or in future beyond tolerance
        if (age > tolerance || age < -tolerance)
        {
            return false;
        }

        var key = $"kyrolus:webhook:seen:{eventId}";

        if (cacheProvider is not null)
        {
            var exists = await cacheProvider.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
            if (exists) return false;

            await cacheProvider.SetAsync(key, true, tolerance * 2, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // Atomic check-and-record (compare-and-swap loop) instead of a separate TryGetValue then
        // write: two concurrent deliveries of the same event id must not both observe "not seen yet".
        var newExpiry = DateTimeOffset.UtcNow.Add(tolerance * 2);
        while (true)
        {
            if (_seenEvents.TryGetValue(eventId, out var existingExpiry))
            {
                if (DateTimeOffset.UtcNow <= existingExpiry)
                {
                    return false; // Replay attack detected
                }

                if (_seenEvents.TryUpdate(eventId, newExpiry, existingExpiry))
                {
                    return true;
                }
            }
            else if (_seenEvents.TryAdd(eventId, newExpiry))
            {
                return true;
            }
        }
    }
}
