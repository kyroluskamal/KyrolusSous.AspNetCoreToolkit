namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusReserveHoldEntry
{
    public required string EntryId { get; init; }
    public required string MerchantId { get; init; }
    public required string SourceTransactionId { get; init; }
    public required decimal HeldAmount { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset HeldAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ReleaseScheduledAtUtc { get; init; }
    public bool IsReleased { get; init; } = false;
}

public sealed record KyrolusReserveStatusSummary
{
    public required string MerchantId { get; init; }
    public decimal TotalLockedReserveAmount { get; init; }
    public decimal TotalReleasedReserveAmount { get; init; }
    public string Currency { get; init; } = "USD";
    public int ActiveHoldCount { get; init; }
}
