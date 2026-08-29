namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusCryptoNetwork
{
    Tron_TRC20,
    Ethereum_ERC20,
    Polygon,
    Solana,
    Bitcoin
}

public enum KyrolusCryptoPaymentStatus
{
    AwaitingDeposit,
    Confirming,
    Completed,
    Underpaid,
    Expired
}

public sealed record KyrolusCreateCryptoPaymentRequest
{
    public required string OrderId { get; init; }
    public required decimal FiatAmount { get; init; }
    public required string FiatCurrency { get; init; } // e.g. "USD"
    public required string CryptoCurrency { get; init; } // e.g. "USDT", "USDC", "BTC"
    public required KyrolusCryptoNetwork Network { get; init; }
    public TimeSpan? ExpiresIn { get; init; }
}

public sealed record KyrolusCryptoPaymentResult
{
    public required string PaymentId { get; init; }
    public required string OrderId { get; init; }
    public required string DepositAddress { get; init; }
    public required decimal RequiredCryptoAmount { get; init; }
    public required string CryptoCurrency { get; init; }
    public required KyrolusCryptoNetwork Network { get; init; }
    public required KyrolusCryptoPaymentStatus Status { get; init; }
    public string? QrCodePayload { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
}
