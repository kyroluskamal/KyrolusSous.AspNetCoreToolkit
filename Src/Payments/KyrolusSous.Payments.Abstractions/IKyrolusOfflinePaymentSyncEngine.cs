namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusOfflinePaymentSyncEngine
{
    void EnqueueOfflineTransaction(KyrolusOfflineTransaction transaction);
    Task<KyrolusOfflineSyncResult> SyncPendingTransactionsAsync(CancellationToken cancellationToken = default);
}
