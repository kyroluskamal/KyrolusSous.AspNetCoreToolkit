namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusSmartPaymentRouter
{
    IKyrolusPaymentProvider ResolveBestProvider(KyrolusPaymentRequest request);
    Task<KyrolusPaymentResult> ExecuteWithFailoverAsync(
        KyrolusPaymentRequest request,
        IReadOnlyList<string>? preferredProviderOrder = null,
        CancellationToken cancellationToken = default);
}
