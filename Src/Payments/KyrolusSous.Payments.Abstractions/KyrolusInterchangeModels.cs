namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusInterchangePricingRequest
{
    public required decimal TransactionAmount { get; init; }
    public required string Currency { get; init; }
    public required KyrolusCardScheme Scheme { get; init; } // Visa, Mastercard, Meeza
    public required KyrolusCardType CardType { get; init; } // Credit, Debit, Commercial
    public bool IsCrossBorder { get; init; } = false;
    public decimal AcquirerMarkupPercent { get; init; } = 0.5m; // 0.50%
    public decimal AcquirerFixedFee { get; init; } = 0.10m; // $0.10
}

public sealed record KyrolusInterchangeFeeBreakdown
{
    public required decimal TransactionAmount { get; init; }
    public required decimal InterchangeFee { get; init; }
    public required decimal SchemeAssessmentFee { get; init; }
    public required decimal AcquirerMarkupFee { get; init; }
    public required decimal TotalProcessingCost { get; init; }
    public required decimal NetSettlementAmount { get; init; }
    public required decimal EffectiveRatePercent { get; init; }
}
