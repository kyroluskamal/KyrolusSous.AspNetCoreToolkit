namespace KyrolusSous.Payments.Abstractions;

public static class KyrolusCurrencyHelper
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private static readonly HashSet<string> ThreeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "JOD", "KWD", "OMR", "TND", "IQD", "LYD"
    };

    public static int GetDecimalPlaces(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return 2;

        var code = currency.Trim().ToUpperInvariant();
        if (ZeroDecimalCurrencies.Contains(code)) return 0;
        if (ThreeDecimalCurrencies.Contains(code)) return 3;
        return 2;
    }

    public static long ToSmallestUnit(decimal amount, string currency)
    {
        var decimals = GetDecimalPlaces(currency);
        var multiplier = (decimal)Math.Pow(10, decimals);
        return (long)Math.Round(amount * multiplier, MidpointRounding.AwayFromZero);
    }

    public static decimal FromSmallestUnit(long smallestUnitAmount, string currency)
    {
        var decimals = GetDecimalPlaces(currency);
        var divisor = (decimal)Math.Pow(10, decimals);
        return smallestUnitAmount / divisor;
    }
}
