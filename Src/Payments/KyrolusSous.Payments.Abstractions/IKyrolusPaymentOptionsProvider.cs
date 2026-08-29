namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentOptionsProvider<TOptions> where TOptions : class
{
    ValueTask<TOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default);
}

public interface IKyrolusPaymentEventHandler<in TEvent> where TEvent : KyrolusWebhookEvent
{
    Task HandleAsync(TEvent webhookEvent, CancellationToken cancellationToken = default);
}
