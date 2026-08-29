using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultTaxCalculationEngine : IKyrolusTaxCalculationEngine
{
    private readonly ConcurrentDictionary<string, (decimal Rate, string Jurisdiction)> _customRates = new(StringComparer.OrdinalIgnoreCase);

    public void SetCustomTaxRate(string countryCode, decimal ratePercent, string jurisdictionName)
    {
        _customRates[countryCode] = (ratePercent, jurisdictionName);
    }

    public KyrolusTaxCalculationResult CalculateTax(KyrolusTaxCalculationRequest request)
    {
        var country = request.CountryCode.Trim().ToUpperInvariant();

        // 1. Check B2B Reverse charge for EU countries
        if (request.IsB2BWithValidVatNumber && IsEuCountry(country))
        {
            return new KyrolusTaxCalculationResult
            {
                TaxableAmount = request.Amount,
                TaxRatePercent = 0m,
                TaxAmount = 0m,
                TotalAmountWithTax = request.Amount,
                JurisdictionName = $"{country} (EU B2B Reverse Charge)",
                IsReverseChargeApplied = true
            };
        }

        // 2. Custom registered overrides
        if (_customRates.TryGetValue(country, out var custom))
        {
            var customTax = Math.Round(request.Amount * (custom.Rate / 100m), 2);
            return new KyrolusTaxCalculationResult
            {
                TaxableAmount = request.Amount,
                TaxRatePercent = custom.Rate,
                TaxAmount = customTax,
                TotalAmountWithTax = request.Amount + customTax,
                JurisdictionName = custom.Jurisdiction,
                IsReverseChargeApplied = false
            };
        }

        // 3. Default standard statutory rates
        var (rate, jurisdiction) = country switch
        {
            "EG" => (14.0m, "Egypt Standard VAT"),
            "SA" => (15.0m, "Saudi Arabia VAT"),
            "AE" => (5.0m, "UAE VAT"),
            "GB" or "UK" => (20.0m, "United Kingdom VAT"),
            "DE" => (19.0m, "Germany VAT (MwSt)"),
            "FR" => (20.0m, "France VAT (TVA)"),
            "ES" => (21.0m, "Spain VAT (IVA)"),
            "IT" => (22.0m, "Italy VAT (IVA)"),
            "NL" => (21.0m, "Netherlands VAT (BTW)"),
            "CA" => (5.0m, "Canada Federal GST"),
            "AU" => (10.0m, "Australia GST"),
            "US" => (8.25m, "US State & Local Combined Average"),
            _ => (0.0m, "Standard Zero-Rated Export")
        };

        var taxAmount = Math.Round(request.Amount * (rate / 100m), 2);
        var total = request.Amount + taxAmount;

        return new KyrolusTaxCalculationResult
        {
            TaxableAmount = request.Amount,
            TaxRatePercent = rate,
            TaxAmount = taxAmount,
            TotalAmountWithTax = total,
            JurisdictionName = jurisdiction,
            IsReverseChargeApplied = false
        };
    }

    private static bool IsEuCountry(string code) => code is "DE" or "FR" or "IT" or "ES" or "NL" or "BE" or "SE" or "PL" or "AT" or "IE" or "PT" or "GR" or "RO" or "CZ";
}
