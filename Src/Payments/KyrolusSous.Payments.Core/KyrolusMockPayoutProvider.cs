using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockPayoutProvider : IKyrolusPayoutProvider
{
    public string ProviderName => "Mock";
    private readonly ConcurrentDictionary<string, KyrolusPayoutResult> _payouts = new();

    public Task<KyrolusPayoutResult> SendPayoutAsync(KyrolusPayoutRequest request, CancellationToken cancellationToken = default)
    {
        var result = new KyrolusPayoutResult
        {
            PayoutId = request.PayoutId,
            ProviderPayoutId = $"po_{Guid.NewGuid():N}",
            Status = KyrolusPayoutStatus.Paid,
            Amount = request.Amount,
            Currency = request.Currency,
            FeeAmount = Math.Round(request.Amount * 0.01m, 2) // 1% fee
        };

        _payouts[request.PayoutId] = result;
        return Task.FromResult(result);
    }

    public async Task<KyrolusBatchPayoutResult> SendBatchPayoutAsync(KyrolusBatchPayoutRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<KyrolusPayoutResult>();
        foreach (var item in request.Payouts)
        {
            var res = await SendPayoutAsync(item, cancellationToken).ConfigureAwait(false);
            results.Add(res);
        }

        return new KyrolusBatchPayoutResult
        {
            BatchId = request.BatchId,
            TotalCount = results.Count,
            SucceededCount = results.Count(r => r.Status == KyrolusPayoutStatus.Paid),
            FailedCount = results.Count(r => r.Status == KyrolusPayoutStatus.Failed),
            Results = results.AsReadOnly()
        };
    }

    public Task<KyrolusPayoutResult> GetPayoutStatusAsync(string payoutId, CancellationToken cancellationToken = default)
    {
        if (_payouts.TryGetValue(payoutId, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new KyrolusPayoutResult
        {
            PayoutId = payoutId,
            ProviderPayoutId = string.Empty,
            Status = KyrolusPayoutStatus.Failed,
            Amount = 0m,
            Currency = "USD",
            ErrorMessage = "Payout not found."
        });
    }

    public Task<bool> CancelPayoutAsync(string payoutId, CancellationToken cancellationToken = default)
    {
        if (_payouts.TryGetValue(payoutId, out var result))
        {
            _payouts[payoutId] = result with { Status = KyrolusPayoutStatus.Cancelled };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
