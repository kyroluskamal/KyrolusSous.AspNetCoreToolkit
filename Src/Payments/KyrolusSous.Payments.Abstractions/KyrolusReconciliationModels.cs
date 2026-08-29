namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusSettlementRecord
{
    public required string TransactionId { get; init; }
    public required decimal SettledAmount { get; init; }
    public required decimal FeeAmount { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset SettledAtUtc { get; init; }
}

public sealed record KyrolusInternalTransactionRecord
{
    public required string TransactionId { get; init; }
    public required decimal ExpectedAmount { get; init; }
    public required string Currency { get; init; }
}

public sealed record KyrolusReconciliationDiscrepancy
{
    public required string TransactionId { get; init; }
    public required string Reason { get; init; }
    public decimal ExpectedAmount { get; init; }
    public decimal ActualAmount { get; init; }
}

public sealed record KyrolusReconciliationReport
{
    public required string BatchId { get; init; }
    public required int TotalMatched { get; init; }
    public required int DiscrepancyCount { get; init; }
    public decimal TotalSettledAmount { get; init; }
    public decimal TotalFeesAmount { get; init; }
    public IReadOnlyList<KyrolusReconciliationDiscrepancy> Discrepancies { get; init; } = [];
    public bool IsFullyReconciled => DiscrepancyCount == 0;
}
