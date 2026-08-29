using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockDisputeProvider : IKyrolusDisputeProvider
{
    public string ProviderName => "Mock";
    private readonly ConcurrentDictionary<string, KyrolusDispute> _disputes = new();

    public KyrolusMockDisputeProvider()
    {
        var sample = new KyrolusDispute
        {
            DisputeId = "dp_sample_123",
            TransactionId = "tx_sample_456",
            Amount = 150m,
            Currency = "USD",
            Reason = "fraudulent",
            Status = KyrolusDisputeStatus.NeedsResponse,
            DueByUtc = DateTimeOffset.UtcNow.AddDays(7)
        };
        _disputes[sample.DisputeId] = sample;
    }

    public Task<KyrolusDispute?> GetDisputeAsync(string disputeId, CancellationToken cancellationToken = default)
    {
        _disputes.TryGetValue(disputeId, out var d);
        return Task.FromResult(d);
    }

    public Task<IReadOnlyList<KyrolusDispute>> ListDisputesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<KyrolusDispute>>(_disputes.Values.ToList().AsReadOnly());
    }

    public Task<KyrolusDisputeEvidenceResult> SubmitEvidenceAsync(KyrolusSubmitDisputeEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (_disputes.TryGetValue(request.DisputeId, out var d))
        {
            var updated = d with { Status = KyrolusDisputeStatus.UnderReview };
            _disputes[request.DisputeId] = updated;

            return Task.FromResult(new KyrolusDisputeEvidenceResult
            {
                DisputeId = request.DisputeId,
                Status = KyrolusDisputeStatus.UnderReview,
                IsSubmitted = true,
                Message = "Evidence submitted successfully. Awaiting bank decision."
            });
        }

        return Task.FromResult(new KyrolusDisputeEvidenceResult
        {
            DisputeId = request.DisputeId,
            Status = KyrolusDisputeStatus.NeedsResponse,
            IsSubmitted = false,
            Message = "Dispute not found."
        });
    }

    public Task<bool> AcceptDisputeAsync(string disputeId, CancellationToken cancellationToken = default)
    {
        if (_disputes.TryGetValue(disputeId, out var d))
        {
            _disputes[disputeId] = d with { Status = KyrolusDisputeStatus.Accepted };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
