using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultLoyaltyRewardsEngine : IKyrolusLoyaltyRewardsEngine
{
    private readonly ConcurrentDictionary<string, decimal> _balances = new(StringComparer.OrdinalIgnoreCase);

    public void AwardPoints(string customerId, decimal transactionAmount, decimal pointsPerUnitCurrency = 1.0m)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return;

        var points = Math.Round(transactionAmount * pointsPerUnitCurrency, 0);
        _balances.AddOrUpdate(customerId, points, (_, cur) => cur + points);
    }

    public decimal GetBalance(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return 0m;
        _balances.TryGetValue(customerId, out var b);
        return b;
    }

    public KyrolusRedeemPointsResult RedeemPoints(KyrolusRedeemPointsRequest request)
    {
        while (true)
        {
            if (!_balances.TryGetValue(request.CustomerId, out var balance) || balance < request.PointsToRedeem)
            {
                return new KyrolusRedeemPointsResult
                {
                    Succeeded = false,
                    RedeemedPoints = 0m,
                    DiscountAmount = 0m,
                    RemainingPointsBalance = balance,
                    ErrorMessage = $"Insufficient points balance. Current balance: {balance}"
                };
            }

            var newBalance = balance - request.PointsToRedeem;
            if (_balances.TryUpdate(request.CustomerId, newBalance, balance))
            {
                var discount = request.PointsToRedeem * request.PointValueInCurrency;

                return new KyrolusRedeemPointsResult
                {
                    Succeeded = true,
                    RedeemedPoints = request.PointsToRedeem,
                    DiscountAmount = discount,
                    RemainingPointsBalance = newBalance
                };
            }
        }
    }
}
