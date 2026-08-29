namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusOfflineTransaction
{
    public required string LocalTransactionId { get; init; }
    public required string ProviderName { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string EncryptedPaymentPayload { get; init; }
    public DateTimeOffset CapturedOfflineAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool IsSynced { get; init; } = false;
}

public sealed record KyrolusOfflineSyncResult
{
    public required int TotalQueued { get; init; }
    public required int SyncedCount { get; init; }
    public required int FailedCount { get; init; }
    public IReadOnlyList<string> SyncedTransactionIds { get; init; } = [];
}
