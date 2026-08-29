namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusIssueGiftCardRequest
{
    public required decimal InitialBalance { get; init; }
    public required string Currency { get; init; }
    public string? RecipientEmail { get; init; }
    public TimeSpan? ValidityPeriod { get; init; }
}

public sealed record KyrolusGiftCard
{
    public required string CardCode { get; init; }
    public required string Pin { get; init; }
    public required decimal CurrentBalance { get; init; }
    public required string Currency { get; init; }
    public required bool IsActive { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
}

public sealed record KyrolusRedeemGiftCardResult
{
    public required bool Succeeded { get; init; }
    public decimal RedeemedAmount { get; init; }
    public decimal RemainingCardBalance { get; init; }
    public string? ErrorMessage { get; init; }
}
