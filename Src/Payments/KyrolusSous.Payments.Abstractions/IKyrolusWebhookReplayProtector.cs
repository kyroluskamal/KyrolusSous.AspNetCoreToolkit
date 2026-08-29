namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusWebhookReplayProtector
{
    Task<bool> ValidateAndRecordWebhookAsync(
        string eventId,
        DateTimeOffset eventTimestampUtc,
        TimeSpan? toleranceWindow = null,
        CancellationToken cancellationToken = default);
}
