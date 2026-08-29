namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusLoyaltyAccount
{
    public required string CustomerId { get; init; }
    public decimal PointsBalance { get; init; }
    public decimal LifetimeEarnedPoints { get; init; }
    public decimal LifetimeRedeemedPoints { get; init; }
}

public sealed record KyrolusRedeemPointsRequest
{
    public required string CustomerId { get; init; }
    public required decimal PointsToRedeem { get; init; }
    public decimal PointValueInCurrency { get; init; } = 0.01m; // 100 points = $1.00
    public string Currency { get; init; } = "USD";
}

public sealed record KyrolusRedeemPointsResult
{
    public required bool Succeeded { get; init; }
    public decimal RedeemedPoints { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal RemainingPointsBalance { get; init; }
    public string? ErrorMessage { get; init; }
}
