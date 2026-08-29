using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultPaymentMetricsCollector : IKyrolusPaymentMetricsCollector
{
    private sealed class MetricsAccumulator
    {
        public long TotalTransactions;
        public long SuccessfulTransactions;
        public long FailedTransactions;
        public double TotalLatencyMs;
        public decimal TotalVolume;
    }

    private readonly ConcurrentDictionary<string, MetricsAccumulator> _metrics = new(StringComparer.OrdinalIgnoreCase);

    public void RecordTransaction(string providerName, bool isSuccess, double latencyMs, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return;

        var acc = _metrics.GetOrAdd(providerName, _ => new MetricsAccumulator());
        lock (acc)
        {
            acc.TotalTransactions++;
            if (isSuccess) acc.SuccessfulTransactions++;
            else acc.FailedTransactions++;
            acc.TotalLatencyMs += latencyMs;
            acc.TotalVolume += amount;
        }
    }

    public KyrolusGatewayHealthReport GetReport(string providerName)
    {
        if (_metrics.TryGetValue(providerName, out var acc))
        {
            lock (acc)
            {
                var avgLatency = acc.TotalTransactions > 0 ? acc.TotalLatencyMs / acc.TotalTransactions : 0.0;
                return new KyrolusGatewayHealthReport
                {
                    ProviderName = providerName,
                    TotalTransactions = acc.TotalTransactions,
                    SuccessfulTransactions = acc.SuccessfulTransactions,
                    FailedTransactions = acc.FailedTransactions,
                    AverageLatencyMs = Math.Round(avgLatency, 2),
                    TotalVolume = acc.TotalVolume
                };
            }
        }

        return new KyrolusGatewayHealthReport
        {
            ProviderName = providerName,
            TotalTransactions = 0,
            SuccessfulTransactions = 0,
            FailedTransactions = 0,
            AverageLatencyMs = 0,
            TotalVolume = 0m
        };
    }

    public IReadOnlyList<KyrolusGatewayHealthReport> GetAllReports()
    {
        return _metrics.Keys.Select(GetReport).ToList().AsReadOnly();
    }
}
