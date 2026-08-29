using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultOfflinePaymentSyncEngine(
    IKyrolusPaymentFactory paymentFactory,
    ILogger<KyrolusDefaultOfflinePaymentSyncEngine>? logger = null) : IKyrolusOfflinePaymentSyncEngine
{
    private readonly ConcurrentQueue<KyrolusOfflineTransaction> _queue = new();

    public void EnqueueOfflineTransaction(KyrolusOfflineTransaction transaction)
    {
        _queue.Enqueue(transaction);
    }

    public async Task<KyrolusOfflineSyncResult> SyncPendingTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var synced = new List<string>();
        int failed = 0;
        int initialCount = _queue.Count;

        while (_queue.TryDequeue(out var tx))
        {
            try
            {
                var provider = paymentFactory.GetProvider(tx.ProviderName);
                var paymentReq = new KyrolusPaymentRequest
                {
                    OrderId = tx.LocalTransactionId,
                    Amount = tx.Amount,
                    Currency = tx.Currency,
                    Description = "Offline POS Synced Payment"
                };

                var res = await provider.CreatePaymentAsync(paymentReq, cancellationToken).ConfigureAwait(false);
                if (res.IsSuccess)
                {
                    synced.Add(tx.LocalTransactionId);
                }
                else
                {
                    failed++;
                    logger?.LogWarning("Failed to sync offline transaction {TxId}: {Error}", tx.LocalTransactionId, res.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                failed++;
                logger?.LogError(ex, "Exception syncing offline transaction {TxId}", tx.LocalTransactionId);
            }
        }

        return new KyrolusOfflineSyncResult
        {
            TotalQueued = initialCount,
            SyncedCount = synced.Count,
            FailedCount = failed,
            SyncedTransactionIds = synced.AsReadOnly()
        };
    }
}
