namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusDisputeProvider
{
    string ProviderName { get; }
    Task<KyrolusDispute?> GetDisputeAsync(string disputeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusDispute>> ListDisputesAsync(CancellationToken cancellationToken = default);
    Task<KyrolusDisputeEvidenceResult> SubmitEvidenceAsync(KyrolusSubmitDisputeEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<bool> AcceptDisputeAsync(string disputeId, CancellationToken cancellationToken = default);
}
