namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusFxRateProvider
{
    void SetRate(string baseCurrency, string targetCurrency, decimal rate);
    Task<KyrolusFxConversionResult> ConvertCurrencyAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);
}
