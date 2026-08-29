namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusLoyaltyRewardsEngine
{
    void AwardPoints(string customerId, decimal transactionAmount, decimal pointsPerUnitCurrency = 1.0m);
    decimal GetBalance(string customerId);
    KyrolusRedeemPointsResult RedeemPoints(KyrolusRedeemPointsRequest request);
}
