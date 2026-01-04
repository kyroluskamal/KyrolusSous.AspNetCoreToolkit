namespace KyrolusSous.DataProtection.Abstractions;

public sealed record KyrolusKeyRingRefreshSignal(
    string ApplicationName,
    string InstanceId,
    DateTimeOffset OccurredAt,
    KyrolusKeyRingRefreshReason Reason);
