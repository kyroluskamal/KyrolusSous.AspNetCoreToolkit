using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultReconciliationEngine : IKyrolusReconciliationEngine
{
    public KyrolusReconciliationReport ReconcileBatch(
        string batchId,
        IReadOnlyList<KyrolusInternalTransactionRecord> internalRecords,
        IReadOnlyList<KyrolusSettlementRecord> settlementRecords)
    {
        var settlementMap = settlementRecords.ToDictionary(s => s.TransactionId, s => s, StringComparer.OrdinalIgnoreCase);
        var discrepancies = new List<KyrolusReconciliationDiscrepancy>();
        int matched = 0;
        decimal totalSettled = 0m;
        decimal totalFees = 0m;

        foreach (var internalRecord in internalRecords)
        {
            if (settlementMap.TryGetValue(internalRecord.TransactionId, out var settlement))
            {
                totalSettled += settlement.SettledAmount;
                totalFees += settlement.FeeAmount;

                if (settlement.SettledAmount != internalRecord.ExpectedAmount)
                {
                    discrepancies.Add(new KyrolusReconciliationDiscrepancy
                    {
                        TransactionId = internalRecord.TransactionId,
                        Reason = $"Amount mismatch: Expected {internalRecord.ExpectedAmount} but settled {settlement.SettledAmount}",
                        ExpectedAmount = internalRecord.ExpectedAmount,
                        ActualAmount = settlement.SettledAmount
                    });
                }
                else
                {
                    matched++;
                }

                settlementMap.Remove(internalRecord.TransactionId);
            }
            else
            {
                discrepancies.Add(new KyrolusReconciliationDiscrepancy
                {
                    TransactionId = internalRecord.TransactionId,
                    Reason = "Transaction missing from gateway settlement batch",
                    ExpectedAmount = internalRecord.ExpectedAmount,
                    ActualAmount = 0m
                });
            }
        }

        // Any leftover in settlementMap was unexpected
        foreach (var leftover in settlementMap.Values)
        {
            totalSettled += leftover.SettledAmount;
            totalFees += leftover.FeeAmount;

            discrepancies.Add(new KyrolusReconciliationDiscrepancy
            {
                TransactionId = leftover.TransactionId,
                Reason = "Unexpected transaction in settlement batch not found in internal records",
                ExpectedAmount = 0m,
                ActualAmount = leftover.SettledAmount
            });
        }

        return new KyrolusReconciliationReport
        {
            BatchId = batchId,
            TotalMatched = matched,
            DiscrepancyCount = discrepancies.Count,
            TotalSettledAmount = totalSettled,
            TotalFeesAmount = totalFees,
            Discrepancies = discrepancies.AsReadOnly()
        };
    }
}
