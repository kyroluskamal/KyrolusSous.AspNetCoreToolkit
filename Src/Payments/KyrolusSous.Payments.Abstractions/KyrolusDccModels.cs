namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusDccQuoteRequest
{
    public required decimal BaseAmount { get; init; }
    public required string BaseCurrency { get; init; }
    public required string CardholderHomeCurrency { get; init; }
    public decimal MarkupMarginPercent { get; init; } = 3.5m;
}

public sealed record KyrolusDccQuoteResult
{
    public required decimal OriginalBaseAmount { get; init; }
    public required string BaseCurrency { get; init; }
    public required decimal ConvertedCardholderAmount { get; init; }
    public required string CardholderCurrency { get; init; }
    public required decimal GuaranteedExchangeRate { get; init; }
    public required decimal AppliedMarginPercent { get; init; }
    public DateTimeOffset QuoteValidUntilUtc { get; init; }
}
