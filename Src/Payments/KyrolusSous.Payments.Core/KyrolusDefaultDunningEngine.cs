using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultDunningEngine : IKyrolusDunningEngine
{
    private static readonly KyrolusDunningPlan DefaultPlan = new();

    public KyrolusDunningEvaluationResult EvaluateNextAction(
        KyrolusDunningAttemptRequest request,
        KyrolusDunningPlan? customPlan = null)
    {
        var plan = customPlan ?? DefaultPlan;

        if (request.CurrentAttemptNumber >= plan.MaxRetryAttempts)
        {
            return new KyrolusDunningEvaluationResult
            {
                SubscriptionId = request.SubscriptionId,
                AttemptNumber = request.CurrentAttemptNumber,
                NextAction = plan.AutoCancelAfterMaxRetries ? KyrolusDunningAction.CancelSubscription : KyrolusDunningAction.PauseSubscription,
                NextRetryUtc = null,
                ShouldNotifyCustomer = true,
                Message = $"Maximum dunning retry attempts ({plan.MaxRetryAttempts}) exceeded. Subscription will be marked as cancelled."
            };
        }

        var index = Math.Min(request.CurrentAttemptNumber - 1, plan.RetryIntervals.Count - 1);
        var interval = index >= 0 && index < plan.RetryIntervals.Count
            ? plan.RetryIntervals[index]
            : TimeSpan.FromDays(2);

        var nextRetry = DateTimeOffset.UtcNow.Add(interval);

        return new KyrolusDunningEvaluationResult
        {
            SubscriptionId = request.SubscriptionId,
            AttemptNumber = request.CurrentAttemptNumber,
            NextAction = KyrolusDunningAction.RetryPayment,
            NextRetryUtc = nextRetry,
            ShouldNotifyCustomer = true,
            Message = $"Payment attempt #{request.CurrentAttemptNumber} failed. Scheduled retry #{request.CurrentAttemptNumber + 1} for {nextRetry:u}."
        };
    }
}
