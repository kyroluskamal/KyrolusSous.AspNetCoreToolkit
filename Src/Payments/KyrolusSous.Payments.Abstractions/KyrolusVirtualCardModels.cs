namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusVirtualCardStatus
{
    Active,
    Frozen,
    Closed,
    Expired
}

public sealed record KyrolusCreateVirtualCardRequest
{
    public required string CardHolderName { get; init; }
    public required decimal SpendingLimit { get; init; }
    public required string Currency { get; init; }
    public bool SingleUseOnly { get; init; } = true;
    public TimeSpan? ValidForDuration { get; init; }
    public string? MerchantRestrictionName { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusVirtualCardResult
{
    public required string CardId { get; init; }
    public required string CardNumber { get; init; }
    public required string Cvv { get; init; }
    public required int ExpirationMonth { get; init; }
    public required int ExpirationYear { get; init; }
    public required decimal SpendingLimit { get; init; }
    public decimal SpentAmount { get; init; } = 0m;
    public required string Currency { get; init; }
    public required KyrolusVirtualCardStatus Status { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
