namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentLinkProvider
{
    string ProviderName { get; }
    Task<KyrolusPaymentLinkResult> CreatePaymentLinkAsync(KyrolusPaymentLinkRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivatePaymentLinkAsync(string linkId, CancellationToken cancellationToken = default);
}
