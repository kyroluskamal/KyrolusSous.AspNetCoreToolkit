namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusNetworkTokenizationEngine
{
    Task<KyrolusNetworkTokenResult> TokenizeCardAsync(
        KyrolusTokenizePanRequest request,
        CancellationToken cancellationToken = default);

    Task<string> GenerateCryptogramForPaymentAsync(
        string tokenReferenceId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);
}
