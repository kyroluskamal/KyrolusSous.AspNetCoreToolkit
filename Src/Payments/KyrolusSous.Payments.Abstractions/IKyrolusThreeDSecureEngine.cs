namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusThreeDSecureEngine
{
    KyrolusThreeDSecureEvaluationResult EvaluateRiskAndFlow(KyrolusThreeDSecureEvaluationRequest request);
}
