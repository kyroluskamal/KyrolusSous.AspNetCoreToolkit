namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentMetricsCollector
{
    void RecordTransaction(string providerName, bool isSuccess, double latencyMs, decimal amount);
    KyrolusGatewayHealthReport GetReport(string providerName);
    IReadOnlyList<KyrolusGatewayHealthReport> GetAllReports();
}
