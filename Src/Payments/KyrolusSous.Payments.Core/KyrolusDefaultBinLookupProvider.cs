using System.Text.RegularExpressions;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultBinLookupProvider(IKyrolusCacheProvider? cacheProvider = null) : IKyrolusBinLookupProvider
{
    public async Task<KyrolusBinLookupResult> LookupBinAsync(string binOrCardNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(binOrCardNumber))
        {
            return new KyrolusBinLookupResult
            {
                Bin = string.Empty,
                Scheme = KyrolusCardScheme.Unknown,
                CardType = KyrolusCardType.Unknown
            };
        }

        var digits = Regex.Replace(binOrCardNumber, @"\D", "");
        var bin = digits.Length >= 6 ? digits[..6] : digits;

        var cacheKey = $"kyrolus:bin:{bin}";
        if (cacheProvider is not null)
        {
            var cached = await cacheProvider.GetAsync<KyrolusBinLookupResult>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null) return cached;
        }

        var result = ResolveSchemeAndType(bin);

        if (cacheProvider is not null)
        {
            await cacheProvider.SetAsync(cacheKey, result, TimeSpan.FromDays(30), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static KyrolusBinLookupResult ResolveSchemeAndType(string bin)
    {
        if (bin.StartsWith("5078") || bin.StartsWith("5079") || bin.StartsWith("5060"))
        {
            return new KyrolusBinLookupResult
            {
                Bin = bin,
                Scheme = KyrolusCardScheme.Meeza,
                CardType = KyrolusCardType.Prepaid,
                BankName = "Egyptian National Payment Scheme (Meeza)",
                CountryCode = "EG",
                CountryName = "Egypt"
            };
        }

        if (bin.StartsWith('4'))
        {
            return new KyrolusBinLookupResult
            {
                Bin = bin,
                Scheme = KyrolusCardScheme.Visa,
                CardType = KyrolusCardType.Credit,
                CountryCode = "US",
                CountryName = "United States"
            };
        }

        if (bin.StartsWith('5') || (bin.Length >= 2 && int.TryParse(bin[..2], out var two) && two is >= 51 and <= 55))
        {
            return new KyrolusBinLookupResult
            {
                Bin = bin,
                Scheme = KyrolusCardScheme.Mastercard,
                CardType = KyrolusCardType.Debit,
                CountryCode = "US",
                CountryName = "United States"
            };
        }

        if (bin.StartsWith("34") || bin.StartsWith("37"))
        {
            return new KyrolusBinLookupResult
            {
                Bin = bin,
                Scheme = KyrolusCardScheme.AmericanExpress,
                CardType = KyrolusCardType.Credit,
                CountryCode = "US",
                CountryName = "United States"
            };
        }

        return new KyrolusBinLookupResult
        {
            Bin = bin,
            Scheme = KyrolusCardScheme.Unknown,
            CardType = KyrolusCardType.Unknown
        };
    }
}
