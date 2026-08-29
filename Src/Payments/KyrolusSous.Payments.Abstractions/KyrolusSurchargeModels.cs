namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusSurchargeEvaluationRequest
{
    public required decimal OrderAmount { get; init; }
    public required string Currency { get; init; }
    public required string CountryCode { get; init; } // e.g. "US", "GB", "EG"
    public required KyrolusCardType CardType { get; init; } // Credit, Debit
    public decimal RequestedSurchargePercent { get; init; } = 3.0m;
}

public sealed record KyrolusSurchargeEvaluationResult
{
    public required decimal OriginalAmount { get; init; }
    public required decimal AllowedSurchargeRatePercent { get; init; }
    public required decimal SurchargeAmount { get; init; }
    public required decimal FinalCustomerChargeAmount { get; init; }
    public required bool IsSurchargePermitted { get; init; }
    public string? ComplianceNote { get; init; }
}
