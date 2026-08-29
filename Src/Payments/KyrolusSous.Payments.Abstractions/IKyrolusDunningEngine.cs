namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusDunningEngine
{
    KyrolusDunningEvaluationResult EvaluateNextAction(
        KyrolusDunningAttemptRequest request,
        KyrolusDunningPlan? customPlan = null);
}
