namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusDiscountType
{
    Percentage,
    FixedAmount
}

public sealed record KyrolusCoupon
{
    public required string Code { get; init; }
    public required KyrolusDiscountType Type { get; init; }
    public required decimal Value { get; init; } // e.g. 20 for 20% or 50 for 50 EGP
    public string? Currency { get; init; }
    public decimal? MinimumOrderAmount { get; init; }
    public decimal? MaximumDiscountAmount { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public int? MaxUsageCount { get; init; }
    public int CurrentUsageCount { get; init; } = 0;
    public bool IsActive { get; init; } = true;
}

public sealed record KyrolusApplyDiscountRequest
{
    public required string CouponCode { get; init; }
    public required decimal OrderAmount { get; init; }
    public required string Currency { get; init; }
    public string? CustomerId { get; init; }
}

public sealed record KyrolusApplyDiscountResult
{
    public required bool IsValid { get; init; }
    public required string CouponCode { get; init; }
    public decimal OriginalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalAmount { get; init; }
    public string? ErrorMessage { get; init; }
}
