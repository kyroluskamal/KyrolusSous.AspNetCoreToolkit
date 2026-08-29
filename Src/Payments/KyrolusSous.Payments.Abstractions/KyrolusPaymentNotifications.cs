using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusPaymentSucceededNotification(
    string ProviderName,
    string TransactionId,
    decimal Amount,
    string Currency,
    string? OrderId = null,
    string? CustomerId = null,
    DateTimeOffset TimestampUtc = default) : IKyrolusNotification
{
    public DateTimeOffset TimestampUtc { get; init; } = TimestampUtc == default ? DateTimeOffset.UtcNow : TimestampUtc;
}

public sealed record KyrolusPaymentFailedNotification(
    string ProviderName,
    string TransactionId,
    string ErrorMessage,
    decimal? Amount = null,
    string? Currency = null,
    string? OrderId = null,
    DateTimeOffset TimestampUtc = default) : IKyrolusNotification
{
    public DateTimeOffset TimestampUtc { get; init; } = TimestampUtc == default ? DateTimeOffset.UtcNow : TimestampUtc;
}

public sealed record KyrolusPaymentRefundedNotification(
    string ProviderName,
    string RefundId,
    string TransactionId,
    decimal Amount,
    string Currency,
    DateTimeOffset TimestampUtc = default) : IKyrolusNotification
{
    public DateTimeOffset TimestampUtc { get; init; } = TimestampUtc == default ? DateTimeOffset.UtcNow : TimestampUtc;
}

public sealed record KyrolusSubscriptionUpdatedNotification(
    string ProviderName,
    string SubscriptionId,
    string CustomerId,
    KyrolusSubscriptionStatus Status,
    DateTimeOffset TimestampUtc = default) : IKyrolusNotification
{
    public DateTimeOffset TimestampUtc { get; init; } = TimestampUtc == default ? DateTimeOffset.UtcNow : TimestampUtc;
}
