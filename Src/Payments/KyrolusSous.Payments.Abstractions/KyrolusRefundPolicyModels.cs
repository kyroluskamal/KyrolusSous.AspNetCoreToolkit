namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusRefundCalculationRequest
{
    public required decimal OriginalOrderAmount { get; init; }
    public required decimal OriginalShippingCost { get; init; }
    public required DateTimeOffset OrderCompletedAtUtc { get; init; }
    public required decimal RequestedRefundAmount { get; init; }
    public int AllowedRefundWindowDays { get; init; } = 14;
    public decimal RestockingFeePercent { get; init; } = 10.0m;
    public bool IsShippingRefundable { get; init; } = false;
}

public sealed record KyrolusRefundCalculationResult
{
    public required bool IsEligibleForRefund { get; init; }
    public decimal GrossRefundAmount { get; init; }
    public decimal RestockingFeeDeduction { get; init; }
    public decimal NonRefundableShippingDeduction { get; init; }
    public decimal NetApprovedRefundAmount { get; init; }
    public string? IneligibilityReason { get; init; }
}
