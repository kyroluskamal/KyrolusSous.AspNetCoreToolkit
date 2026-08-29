using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultSettlementRouteOptimizer : IKyrolusSettlementRouteOptimizer
{
    public KyrolusSettlementRouteDecision OptimizeSettlementRoute(
        string payoutCurrency,
        IReadOnlyList<KyrolusMerchantBankAccount> availableAccounts)
    {
        if (availableAccounts == null || availableAccounts.Count == 0)
        {
            throw new ArgumentException("At least one merchant bank account must be supplied.", nameof(availableAccounts));
        }

        // 1. Direct matching domestic currency account (Zero cross-border wire fee)
        var perfectMatch = availableAccounts.FirstOrDefault(a => a.Currency.Equals(payoutCurrency, StringComparison.OrdinalIgnoreCase) && a.IsDomestic);
        if (perfectMatch != null)
        {
            return new KyrolusSettlementRouteDecision
            {
                SelectedAccountId = perfectMatch.AccountId,
                SelectedCurrency = perfectMatch.Currency,
                IsDomesticClearing = true,
                EstimatedWireFee = 0.0m,
                RoutingRationale = $"Domestic automated clearing house (ACH/SEPA/IPN) in {payoutCurrency} with 0 wire fee."
            };
        }

        // 2. Currency match (cross-border)
        var currencyMatch = availableAccounts.FirstOrDefault(a => a.Currency.Equals(payoutCurrency, StringComparison.OrdinalIgnoreCase));
        if (currencyMatch != null)
        {
            return new KyrolusSettlementRouteDecision
            {
                SelectedAccountId = currencyMatch.AccountId,
                SelectedCurrency = currencyMatch.Currency,
                IsDomesticClearing = false,
                EstimatedWireFee = 15.0m,
                RoutingRationale = $"Same-currency cross-border transfer into {payoutCurrency} account ($15 standard SWIFT fee)."
            };
        }

        // 3. Fallback to first available account (Incurs FX + Wire fee)
        var fallback = availableAccounts[0];
        return new KyrolusSettlementRouteDecision
        {
            SelectedAccountId = fallback.AccountId,
            SelectedCurrency = fallback.Currency,
            IsDomesticClearing = false,
            EstimatedWireFee = 25.0m,
            RoutingRationale = $"Cross-currency SWIFT wire transfer from {payoutCurrency} into {fallback.Currency} account."
        };
    }
}
