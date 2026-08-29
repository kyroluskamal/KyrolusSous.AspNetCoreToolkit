namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusTaxCalculationEngine
{
    void SetCustomTaxRate(string countryCode, decimal ratePercent, string jurisdictionName);
    KyrolusTaxCalculationResult CalculateTax(KyrolusTaxCalculationRequest request);
}
