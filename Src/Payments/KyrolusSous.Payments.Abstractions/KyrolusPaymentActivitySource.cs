using System.Diagnostics;

namespace KyrolusSous.Payments.Abstractions;

public static class KyrolusPaymentActivitySource
{
    public const string ActivitySourceName = "KyrolusSous.Payments";
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    public static Activity? StartPaymentActivity(string providerName, string operation, string? transactionId = null)
    {
        var activity = Source.StartActivity($"Payment.{providerName}.{operation}", ActivityKind.Client);
        if (activity is not null)
        {
            activity.SetTag("payment.provider", providerName);
            activity.SetTag("payment.operation", operation);
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                activity.SetTag("payment.transaction_id", transactionId);
            }
        }
        return activity;
    }

    public static void RecordSuccess(Activity? activity, decimal? amount = null, string? currency = null)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Ok);
        if (amount.HasValue) activity.SetTag("payment.amount", amount.Value);
        if (!string.IsNullOrWhiteSpace(currency)) activity.SetTag("payment.currency", currency);
    }

    public static void RecordFailure(Activity? activity, string errorMessage)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, errorMessage);
        activity.SetTag("payment.error", errorMessage);
    }
}
