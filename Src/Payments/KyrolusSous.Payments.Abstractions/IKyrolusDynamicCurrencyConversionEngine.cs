namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusDynamicCurrencyConversionEngine
{
    Task<KyrolusDccQuoteResult> GenerateDccQuoteAsync(
        KyrolusDccQuoteRequest request,
        CancellationToken cancellationToken = default);
}
