namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusDunningAction
{
    RetryPayment,
    NotifyCustomer,
    PauseSubscription,
    CancelSubscription
}

public sealed record KyrolusDunningPlan
{
    public int MaxRetryAttempts { get; init; } = 4;
    public IReadOnlyList<TimeSpan> RetryIntervals { get; init; } =
    [
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(5),
        TimeSpan.FromDays(7)
    ];
    public bool AutoCancelAfterMaxRetries { get; init; } = true;
}

public sealed record KyrolusDunningAttemptRequest
{
    public required string SubscriptionId { get; init; }
    public required string CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public int CurrentAttemptNumber { get; init; } = 1;
    public string? LastFailureReason { get; init; }
}

public sealed record KyrolusDunningEvaluationResult
{
    public required string SubscriptionId { get; init; }
    public required int AttemptNumber { get; init; }
    public required KyrolusDunningAction NextAction { get; init; }
    public DateTimeOffset? NextRetryUtc { get; init; }
    public bool ShouldNotifyCustomer { get; init; }
    public string Message { get; init; } = string.Empty;
}
