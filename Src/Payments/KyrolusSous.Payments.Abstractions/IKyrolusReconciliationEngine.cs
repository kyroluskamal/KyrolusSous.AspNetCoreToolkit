namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusReconciliationEngine
{
    KyrolusReconciliationReport ReconcileBatch(
        string batchId,
        IReadOnlyList<KyrolusInternalTransactionRecord> internalRecords,
        IReadOnlyList<KyrolusSettlementRecord> settlementRecords);
}
