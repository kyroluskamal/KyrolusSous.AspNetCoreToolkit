namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusInterchangePlusCalculator
{
    KyrolusInterchangeFeeBreakdown CalculateFeeBreakdown(KyrolusInterchangePricingRequest request);
}
