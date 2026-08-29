namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusWebhookDispatchSubscription
{
    public required string SubscriptionId { get; init; }
    public required string DestinationUrl { get; init; }
    public required string SecretKey { get; init; } // HMAC signature secret
    public IReadOnlyList<string> SubscribedEventTypes { get; init; } = ["*"];
}

public sealed record KyrolusWebhookDeliveryAttemptResult
{
    public required string SubscriptionId { get; init; }
    public required string DestinationUrl { get; init; }
    public required bool Succeeded { get; init; }
    public int HttpStatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset AttemptedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
