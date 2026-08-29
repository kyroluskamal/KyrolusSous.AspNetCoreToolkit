using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockEscrowProvider : IKyrolusEscrowProvider
{
    public string ProviderName => "Mock";
    private readonly ConcurrentDictionary<string, KyrolusEscrowResult> _holds = new();

    public Task<KyrolusEscrowResult> HoldFundsAsync(KyrolusHoldFundsRequest request, CancellationToken cancellationToken = default)
    {
        var duration = request.HoldDuration ?? TimeSpan.FromDays(7);
        var result = new KyrolusEscrowResult
        {
            HoldId = request.HoldId,
            AuthorizationCode = $"AUTH-{Random.Shared.Next(100000, 999999)}",
            Status = KyrolusEscrowStatus.Held,
            Amount = request.Amount,
            Currency = request.Currency,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(duration)
        };

        _holds[request.HoldId] = result;
        return Task.FromResult(result);
    }

    public Task<KyrolusEscrowResult> CaptureHeldFundsAsync(string holdId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        if (_holds.TryGetValue(holdId, out var hold))
        {
            var captureAmount = amount ?? hold.Amount;
            var updated = hold with
            {
                Status = KyrolusEscrowStatus.Captured,
                Amount = captureAmount
            };
            _holds[holdId] = updated;
            return Task.FromResult(updated);
        }

        return Task.FromResult(new KyrolusEscrowResult
        {
            HoldId = holdId,
            AuthorizationCode = string.Empty,
            Status = KyrolusEscrowStatus.Expired,
            Amount = 0m,
            Currency = "USD",
            ErrorMessage = "Escrow hold not found."
        });
    }

    public Task<bool> VoidHeldFundsAsync(string holdId, CancellationToken cancellationToken = default)
    {
        if (_holds.TryGetValue(holdId, out var hold))
        {
            _holds[holdId] = hold with { Status = KyrolusEscrowStatus.Voided };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
