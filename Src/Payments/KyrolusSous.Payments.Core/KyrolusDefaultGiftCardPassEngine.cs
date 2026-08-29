using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultGiftCardPassEngine : IKyrolusGiftCardPassEngine
{
    private readonly ConcurrentDictionary<string, KyrolusGiftCard> _cards = new(StringComparer.OrdinalIgnoreCase);

    public KyrolusGiftCard IssueGiftCard(KyrolusIssueGiftCardRequest request)
    {
        var code = $"GC-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}";
        var pin = $"{Random.Shared.Next(1000, 9999)}";

        var card = new KyrolusGiftCard
        {
            CardCode = code,
            Pin = pin,
            CurrentBalance = request.InitialBalance,
            Currency = request.Currency,
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(request.ValidityPeriod ?? TimeSpan.FromDays(365))
        };

        _cards[code] = card;
        return card;
    }

    public KyrolusRedeemGiftCardResult RedeemGiftCard(string cardCode, string pin, decimal amountToDeduct)
    {
        while (true)
        {
            if (!_cards.TryGetValue(cardCode, out var card) || !card.IsActive || !card.Pin.Equals(pin, StringComparison.Ordinal))
            {
                return new KyrolusRedeemGiftCardResult
                {
                    Succeeded = false,
                    ErrorMessage = "Invalid card code or PIN."
                };
            }

            if (card.CurrentBalance < amountToDeduct)
            {
                return new KyrolusRedeemGiftCardResult
                {
                    Succeeded = false,
                    RemainingCardBalance = card.CurrentBalance,
                    ErrorMessage = $"Insufficient balance. Available: {card.CurrentBalance}"
                };
            }

            var newBalance = card.CurrentBalance - amountToDeduct;
            var updated = card with { CurrentBalance = newBalance };

            if (_cards.TryUpdate(cardCode, updated, card))
            {
                return new KyrolusRedeemGiftCardResult
                {
                    Succeeded = true,
                    RedeemedAmount = amountToDeduct,
                    RemainingCardBalance = newBalance
                };
            }
        }
    }

    public decimal GetBalance(string cardCode, string pin)
    {
        if (_cards.TryGetValue(cardCode, out var card) && card.IsActive && card.Pin.Equals(pin, StringComparison.Ordinal))
        {
            return card.CurrentBalance;
        }
        return 0m;
    }
}
