namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentProvider
{
    string ProviderName { get; }
    IReadOnlyList<string> SupportedCurrencies { get; }
    IReadOnlyList<KyrolusPaymentMethodType> SupportedMethods { get; }

    Task<KyrolusPaymentResult> CreatePaymentAsync(KyrolusPaymentRequest request, CancellationToken cancellationToken = default);
    Task<KyrolusPaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default);
    Task<KyrolusPaymentResult> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<KyrolusRefundResult> RefundPaymentAsync(KyrolusRefundRequest request, CancellationToken cancellationToken = default);
    Task<bool> CancelPaymentAsync(string transactionId, CancellationToken cancellationToken = default);
}
