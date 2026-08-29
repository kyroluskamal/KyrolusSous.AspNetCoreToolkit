using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockVirtualCardProvider : IKyrolusVirtualCardProvider
{
    public string ProviderName => "Mock";
    private readonly ConcurrentDictionary<string, KyrolusVirtualCardResult> _cards = new();

    public Task<KyrolusVirtualCardResult> IssueVirtualCardAsync(
        KyrolusCreateVirtualCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var cardId = $"vc_{Guid.NewGuid():N}";
        var randomDigits = Random.Shared.Next(100000, 999999);
        var cardNumber = $"411111{randomDigits:D6}1111";

        var result = new KyrolusVirtualCardResult
        {
            CardId = cardId,
            CardNumber = cardNumber,
            Cvv = $"{Random.Shared.Next(100, 999)}",
            ExpirationMonth = 12,
            ExpirationYear = DateTime.UtcNow.Year + 2,
            SpendingLimit = request.SpendingLimit,
            SpentAmount = 0m,
            Currency = request.Currency,
            Status = KyrolusVirtualCardStatus.Active
        };

        _cards[cardId] = result;
        return Task.FromResult(result);
    }

    public Task<bool> FreezeCardAsync(string cardId, CancellationToken cancellationToken = default)
    {
        if (_cards.TryGetValue(cardId, out var card))
        {
            _cards[cardId] = card with { Status = KyrolusVirtualCardStatus.Frozen };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> CloseCardAsync(string cardId, CancellationToken cancellationToken = default)
    {
        if (_cards.TryGetValue(cardId, out var card))
        {
            _cards[cardId] = card with { Status = KyrolusVirtualCardStatus.Closed };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
