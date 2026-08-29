namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusEscrowStatus
{
    Held,
    Captured,
    Voided,
    Expired
}

public sealed record KyrolusHoldFundsRequest
{
    public required string HoldId { get; init; }
    public required string CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public TimeSpan? HoldDuration { get; init; }
    public string? Description { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record KyrolusEscrowResult
{
    public required string HoldId { get; init; }
    public required string AuthorizationCode { get; init; }
    public required KyrolusEscrowStatus Status { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
