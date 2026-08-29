namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusFraudDetectionEngine
{
    Task<KyrolusRiskEvaluationResult> EvaluateRiskAsync(
        KyrolusRiskEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
