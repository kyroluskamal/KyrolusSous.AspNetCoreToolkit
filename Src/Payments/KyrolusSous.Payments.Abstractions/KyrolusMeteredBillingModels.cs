namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusUsageRecord
{
    public required string SubscriptionId { get; init; }
    public required string MetricName { get; init; } // e.g. "api_calls", "storage_gb"
    public required decimal Quantity { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record KyrolusMeteredMetricTier
{
    public required string MetricName { get; init; }
    public decimal UnitPrice { get; init; } // e.g. $0.002 per API call
    public decimal IncludedQuantity { get; init; } = 0m;
}

public sealed record KyrolusMeteredBillingSummary
{
    public required string SubscriptionId { get; init; }
    public required string MetricName { get; init; }
    public decimal TotalUsageQuantity { get; init; }
    public decimal BilledQuantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalCost { get; init; }
    public string Currency { get; init; } = "USD";
}
