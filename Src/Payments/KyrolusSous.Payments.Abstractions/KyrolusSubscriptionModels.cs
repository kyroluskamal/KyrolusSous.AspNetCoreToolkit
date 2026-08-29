namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusSubscriptionStatus
{
    Active,
    Trailing,
    Paused,
    Cancelled,
    PastDue,
    Incomplete
}

public enum KyrolusBillingInterval
{
    Day,
    Week,
    Month,
    Year
}

public sealed record KyrolusSubscriptionPlan
{
    public required string PlanId { get; init; }
    public required string Name { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public KyrolusBillingInterval Interval { get; init; } = KyrolusBillingInterval.Month;
    public int IntervalCount { get; init; } = 1;
    public int TrialDays { get; init; } = 0;
    public string? Description { get; init; }
}

public sealed record KyrolusSubscriptionRequest
{
    public required string CustomerId { get; init; }
    public required string PlanId { get; init; }
    public string? PaymentMethodId { get; init; }
    public string? CouponCode { get; init; }
    public int? CustomTrialDays { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusSubscriptionResult
{
    public required string SubscriptionId { get; init; }
    public string? CustomerId { get; init; }
    public string? PlanId { get; init; }
    public required KyrolusSubscriptionStatus Status { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset CurrentPeriodStartUtc { get; init; }
    public DateTimeOffset CurrentPeriodEndUtc { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
    public string? ClientSecret { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => Status is KyrolusSubscriptionStatus.Active or KyrolusSubscriptionStatus.Trailing;
}
