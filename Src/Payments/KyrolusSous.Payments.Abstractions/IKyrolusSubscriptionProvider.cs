namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusSubscriptionProvider
{
    string ProviderName { get; }

    Task<KyrolusSubscriptionPlan> CreatePlanAsync(KyrolusSubscriptionPlan plan, CancellationToken cancellationToken = default);
    Task<KyrolusSubscriptionResult> CreateSubscriptionAsync(KyrolusSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<KyrolusSubscriptionResult> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<KyrolusSubscriptionResult> CancelSubscriptionAsync(string subscriptionId, bool cancelImmediately = false, CancellationToken cancellationToken = default);
    Task<KyrolusSubscriptionResult> PauseSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<KyrolusSubscriptionResult> ResumeSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
}
