using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultDynamicCurrencyConversionEngine(
    IKyrolusFxRateProvider fxRateProvider) : IKyrolusDynamicCurrencyConversionEngine
{
    public async Task<KyrolusDccQuoteResult> GenerateDccQuoteAsync(
        KyrolusDccQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var fxResult = await fxRateProvider.ConvertCurrencyAsync(request.BaseAmount, request.BaseCurrency, request.CardholderHomeCurrency, cancellationToken).ConfigureAwait(false);
        var marginMultiplier = 1m + (request.MarkupMarginPercent / 100m);
        var finalRate = fxResult.ExchangeRate * marginMultiplier;
        var finalAmount = Math.Round(request.BaseAmount * finalRate, 2);

        return new KyrolusDccQuoteResult
        {
            OriginalBaseAmount = request.BaseAmount,
            BaseCurrency = request.BaseCurrency,
            ConvertedCardholderAmount = finalAmount,
            CardholderCurrency = request.CardholderHomeCurrency,
            GuaranteedExchangeRate = Math.Round(finalRate, 4),
            AppliedMarginPercent = request.MarkupMarginPercent,
            QuoteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        };
    }
}
