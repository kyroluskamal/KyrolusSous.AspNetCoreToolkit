using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultMeteredBillingEngine : IKyrolusMeteredBillingEngine
{
    private readonly ConcurrentDictionary<string, decimal> _usageTotals = new(StringComparer.OrdinalIgnoreCase);

    public void RecordUsage(KyrolusUsageRecord record)
    {
        var key = $"{record.SubscriptionId}_{record.MetricName}";
        _usageTotals.AddOrUpdate(key, record.Quantity, (_, current) => current + record.Quantity);
    }

    public KyrolusMeteredBillingSummary CalculateSummary(
        string subscriptionId,
        KyrolusMeteredMetricTier tier,
        string currency = "USD")
    {
        var key = $"{subscriptionId}_{tier.MetricName}";
        _usageTotals.TryGetValue(key, out var totalUsage);

        var billable = Math.Max(0m, totalUsage - tier.IncludedQuantity);
        var cost = Math.Round(billable * tier.UnitPrice, 2);

        return new KyrolusMeteredBillingSummary
        {
            SubscriptionId = subscriptionId,
            MetricName = tier.MetricName,
            TotalUsageQuantity = totalUsage,
            BilledQuantity = billable,
            UnitPrice = tier.UnitPrice,
            TotalCost = cost,
            Currency = currency
        };
    }

    public void ResetUsage(string subscriptionId, string metricName)
    {
        var key = $"{subscriptionId}_{metricName}";
        _usageTotals.TryRemove(key, out _);
    }
}
