namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusGatewayHealthReport
{
    public required string ProviderName { get; init; }
    public long TotalTransactions { get; init; }
    public long SuccessfulTransactions { get; init; }
    public long FailedTransactions { get; init; }
    public double SuccessRatePercent => TotalTransactions > 0
        ? Math.Round((double)SuccessfulTransactions / TotalTransactions * 100, 2)
        : 100.0;
    public double AverageLatencyMs { get; init; }
    public decimal TotalVolume { get; init; }
}
