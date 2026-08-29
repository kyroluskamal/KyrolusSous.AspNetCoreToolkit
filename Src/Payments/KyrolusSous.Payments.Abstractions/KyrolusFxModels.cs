namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusFxRate
{
    public required string BaseCurrency { get; init; }
    public required string TargetCurrency { get; init; }
    public required decimal Rate { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record KyrolusFxConversionResult
{
    public required decimal OriginalAmount { get; init; }
    public required string FromCurrency { get; init; }
    public required string ToCurrency { get; init; }
    public required decimal ConvertedAmount { get; init; }
    public required decimal ExchangeRate { get; init; }
}
