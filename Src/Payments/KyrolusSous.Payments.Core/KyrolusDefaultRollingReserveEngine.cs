using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultRollingReserveEngine : IKyrolusRollingReserveEngine
{
    private readonly ConcurrentDictionary<string, KyrolusReserveHoldEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public KyrolusReserveHoldEntry ApplyReserveHold(
        string merchantId,
        string transactionId,
        decimal transactionGrossAmount,
        string currency,
        decimal holdPercentage = 5.0m,
        TimeSpan? holdDuration = null)
    {
        var duration = holdDuration ?? TimeSpan.FromDays(90);
        var heldAmount = Math.Round(transactionGrossAmount * (holdPercentage / 100m), 2);
        var entryId = $"res_{Guid.NewGuid():N}";

        var entry = new KyrolusReserveHoldEntry
        {
            EntryId = entryId,
            MerchantId = merchantId,
            SourceTransactionId = transactionId,
            HeldAmount = heldAmount,
            Currency = currency,
            ReleaseScheduledAtUtc = DateTimeOffset.UtcNow.Add(duration)
        };

        _entries[entryId] = entry;
        return entry;
    }

    public decimal ReleaseEligibleHolds(string merchantId, DateTimeOffset asOfUtc)
    {
        var eligible = _entries.Values
            .Where(e => e.MerchantId.Equals(merchantId, StringComparison.OrdinalIgnoreCase) && !e.IsReleased && e.ReleaseScheduledAtUtc <= asOfUtc)
            .ToList();

        decimal released = 0m;

        foreach (var item in eligible)
        {
            var updated = item with { IsReleased = true };
            if (_entries.TryUpdate(item.EntryId, updated, item))
            {
                released += item.HeldAmount;
            }
        }

        return released;
    }

    public KyrolusReserveStatusSummary GetReserveSummary(string merchantId, string currency = "USD")
    {
        var merchantEntries = _entries.Values
            .Where(e => e.MerchantId.Equals(merchantId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new KyrolusReserveStatusSummary
        {
            MerchantId = merchantId,
            TotalLockedReserveAmount = merchantEntries.Where(e => !e.IsReleased).Sum(e => e.HeldAmount),
            TotalReleasedReserveAmount = merchantEntries.Where(e => e.IsReleased).Sum(e => e.HeldAmount),
            Currency = currency,
            ActiveHoldCount = merchantEntries.Count(e => !e.IsReleased)
        };
    }
}
