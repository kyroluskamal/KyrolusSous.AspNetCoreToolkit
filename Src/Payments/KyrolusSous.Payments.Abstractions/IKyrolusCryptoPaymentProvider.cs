namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusCryptoPaymentProvider
{
    string ProviderName { get; }
    Task<KyrolusCryptoPaymentResult> CreatePaymentIntentAsync(
        KyrolusCreateCryptoPaymentRequest request,
        CancellationToken cancellationToken = default);
    Task<KyrolusCryptoPaymentResult> CheckPaymentStatusAsync(
        string paymentId,
        CancellationToken cancellationToken = default);
}
