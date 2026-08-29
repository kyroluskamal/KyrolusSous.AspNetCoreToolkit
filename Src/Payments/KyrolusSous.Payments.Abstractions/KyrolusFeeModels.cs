namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusProviderFeeStructure
{
    public required string ProviderName { get; init; }
    public decimal PercentageFee { get; init; } // e.g. 2.9 for 2.9%
    public decimal FixedFee { get; init; } // e.g. 0.30 for 30 cents
    public string Currency { get; init; } = "USD";
}

public sealed record KyrolusFeeOptimizationResult
{
    public required string RecommendedProviderName { get; init; }
    public decimal EstimatedFee { get; init; }
    public decimal NetMerchantAmount { get; init; }
    public IReadOnlyDictionary<string, decimal> AllProviderFees { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
}
