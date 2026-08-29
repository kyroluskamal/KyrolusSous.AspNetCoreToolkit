namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentWebhookDispatcher
{
    void RegisterSubscription(KyrolusWebhookDispatchSubscription subscription);

    Task<IReadOnlyList<KyrolusWebhookDeliveryAttemptResult>> DispatchEventAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
