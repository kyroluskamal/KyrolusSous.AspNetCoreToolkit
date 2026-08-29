namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusMerchantBankAccount
{
    public required string AccountId { get; init; }
    public required string BankCountryCode { get; init; } // "EG", "US", "GB", "DE"
    public required string Currency { get; init; } // "EGP", "USD", "GBP", "EUR"
    public required string IbanOrAccountNumber { get; init; }
    public bool IsDomestic { get; init; } = true;
}

public sealed record KyrolusSettlementRouteDecision
{
    public required string SelectedAccountId { get; init; }
    public required string SelectedCurrency { get; init; }
    public required bool IsDomesticClearing { get; init; }
    public decimal EstimatedWireFee { get; init; }
    public string? RoutingRationale { get; init; }
}
