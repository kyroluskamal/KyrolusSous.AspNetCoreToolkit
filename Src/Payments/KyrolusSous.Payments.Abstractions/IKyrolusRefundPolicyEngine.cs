namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusRefundPolicyEngine
{
    KyrolusRefundCalculationResult CalculateRefund(KyrolusRefundCalculationRequest request);
}
