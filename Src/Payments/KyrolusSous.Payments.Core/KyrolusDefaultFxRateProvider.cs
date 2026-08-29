using System.Collections.Concurrent;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultFxRateProvider(IKyrolusCacheProvider? cacheProvider = null) : IKyrolusFxRateProvider
{
    private readonly ConcurrentDictionary<string, decimal> _rates = new(StringComparer.OrdinalIgnoreCase);

    public KyrolusDefaultFxRateProvider() : this((IKyrolusCacheProvider?)null)
    {
        // Seed standard rates
        SetRate("USD", "EGP", 50.0m);
        SetRate("USD", "EUR", 0.92m);
        SetRate("USD", "SAR", 3.75m);
        SetRate("USD", "AED", 3.67m);
        SetRate("USD", "KWD", 0.31m);
    }

    public void SetRate(string baseCurrency, string targetCurrency, decimal rate)
    {
        var key = $"{baseCurrency.Trim().ToUpperInvariant()}_{targetCurrency.Trim().ToUpperInvariant()}";
        _rates[key] = rate;

        // Invert
        if (rate > 0)
        {
            var invKey = $"{targetCurrency.Trim().ToUpperInvariant()}_{baseCurrency.Trim().ToUpperInvariant()}";
            _rates[invKey] = 1m / rate;
        }
    }

    public async Task<KyrolusFxConversionResult> ConvertCurrencyAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();

        if (from == to)
        {
            return new KyrolusFxConversionResult
            {
                OriginalAmount = amount,
                FromCurrency = from,
                ToCurrency = to,
                ConvertedAmount = amount,
                ExchangeRate = 1.0m
            };
        }

        var pairKey = $"{from}_{to}";

        if (cacheProvider is not null)
        {
            var cachedRate = await cacheProvider.GetAsync<decimal>($"kyrolus:fx:{pairKey}", cancellationToken).ConfigureAwait(false);
            if (cachedRate > 0)
            {
                var converted = Math.Round(amount * cachedRate, 2);
                return new KyrolusFxConversionResult
                {
                    OriginalAmount = amount,
                    FromCurrency = from,
                    ToCurrency = to,
                    ConvertedAmount = converted,
                    ExchangeRate = cachedRate
                };
            }
        }

        if (_rates.TryGetValue(pairKey, out var rate))
        {
            var converted = Math.Round(amount * rate, 2);
            return new KyrolusFxConversionResult
            {
                OriginalAmount = amount,
                FromCurrency = from,
                ToCurrency = to,
                ConvertedAmount = converted,
                ExchangeRate = rate
            };
        }

        throw new KeyNotFoundException($"Exchange rate pair '{pairKey}' is not configured.");
    }
}
