namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusPayoutStatus
{
    Pending,
    Processing,
    Paid,
    Failed,
    Cancelled
}

public enum KyrolusPayoutDestinationType
{
    BankAccount,
    DigitalWallet,
    Card,
    InstantPay
}

public sealed record KyrolusPayoutRequest
{
    public required string PayoutId { get; init; }
    public required string RecipientId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required KyrolusPayoutDestinationType DestinationType { get; init; }
    public required string DestinationAccountIdentifier { get; init; } // IBAN, Wallet Number, or InstaPay IPA
    public string? Description { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusPayoutResult
{
    public required string PayoutId { get; init; }
    public required string ProviderPayoutId { get; init; }
    public required KyrolusPayoutStatus Status { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public decimal FeeAmount { get; init; } = 0m;
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record KyrolusBatchPayoutRequest
{
    public required string BatchId { get; init; }
    public required IReadOnlyList<KyrolusPayoutRequest> Payouts { get; init; }
}

public sealed record KyrolusBatchPayoutResult
{
    public required string BatchId { get; init; }
    public required int TotalCount { get; init; }
    public required int SucceededCount { get; init; }
    public required int FailedCount { get; init; }
    public IReadOnlyList<KyrolusPayoutResult> Results { get; init; } = [];
}
