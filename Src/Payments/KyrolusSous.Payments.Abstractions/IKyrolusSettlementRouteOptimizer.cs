namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusSettlementRouteOptimizer
{
    KyrolusSettlementRouteDecision OptimizeSettlementRoute(
        string payoutCurrency,
        IReadOnlyList<KyrolusMerchantBankAccount> availableAccounts);
}
