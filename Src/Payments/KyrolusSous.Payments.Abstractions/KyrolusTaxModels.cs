namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusTaxCalculationRequest
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string CountryCode { get; init; } // e.g. "EG", "SA", "US", "DE"
    public string? PostalCode { get; init; }
    public string? StateOrProvince { get; init; }
    public bool IsB2BWithValidVatNumber { get; init; } = false;
}

public sealed record KyrolusTaxCalculationResult
{
    public required decimal TaxableAmount { get; init; }
    public required decimal TaxRatePercent { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal TotalAmountWithTax { get; init; }
    public required string JurisdictionName { get; init; }
    public bool IsReverseChargeApplied { get; init; } = false;
}
