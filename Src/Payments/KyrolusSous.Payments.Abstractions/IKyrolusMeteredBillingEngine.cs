namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusMeteredBillingEngine
{
    void RecordUsage(KyrolusUsageRecord record);
    KyrolusMeteredBillingSummary CalculateSummary(
        string subscriptionId,
        KyrolusMeteredMetricTier tier,
        string currency = "USD");
    void ResetUsage(string subscriptionId, string metricName);
}
