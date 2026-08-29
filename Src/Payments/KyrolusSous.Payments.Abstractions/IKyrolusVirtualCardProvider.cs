namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusVirtualCardProvider
{
    string ProviderName { get; }
    Task<KyrolusVirtualCardResult> IssueVirtualCardAsync(
        KyrolusCreateVirtualCardRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> FreezeCardAsync(string cardId, CancellationToken cancellationToken = default);
    Task<bool> CloseCardAsync(string cardId, CancellationToken cancellationToken = default);
}
