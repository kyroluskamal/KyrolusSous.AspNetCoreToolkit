namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusSurchargingEngine
{
    KyrolusSurchargeEvaluationResult CalculateCompliantSurcharge(KyrolusSurchargeEvaluationRequest request);
}
