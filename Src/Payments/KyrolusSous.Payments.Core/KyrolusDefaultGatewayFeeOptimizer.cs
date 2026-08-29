using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultGatewayFeeOptimizer : IKyrolusGatewayFeeOptimizer
{
    private readonly ConcurrentDictionary<string, KyrolusProviderFeeStructure> _structures = new(StringComparer.OrdinalIgnoreCase);

    public KyrolusDefaultGatewayFeeOptimizer()
    {
        // Seed standard fee structures
        RegisterFeeStructure(new KyrolusProviderFeeStructure { ProviderName = "Stripe", PercentageFee = 2.9m, FixedFee = 0.30m, Currency = "USD" });
        RegisterFeeStructure(new KyrolusProviderFeeStructure { ProviderName = "PayPal", PercentageFee = 3.49m, FixedFee = 0.49m, Currency = "USD" });
        RegisterFeeStructure(new KyrolusProviderFeeStructure { ProviderName = "Paymob", PercentageFee = 2.5m, FixedFee = 2.0m, Currency = "EGP" });
        RegisterFeeStructure(new KyrolusProviderFeeStructure { ProviderName = "Fawry", PercentageFee = 2.0m, FixedFee = 0.0m, Currency = "EGP" });
    }

    public void RegisterFeeStructure(KyrolusProviderFeeStructure structure)
    {
        _structures[structure.ProviderName] = structure;
    }

    public KyrolusFeeOptimizationResult OptimizeFee(decimal amount, string currency, IReadOnlyList<string>? candidateProviders = null)
    {
        var providersToEvaluate = candidateProviders is { Count: > 0 }
            ? _structures.Values.Where(s => candidateProviders.Contains(s.ProviderName, StringComparer.OrdinalIgnoreCase)).ToList()
            : _structures.Values.Where(s => s.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase)).ToList();

        if (providersToEvaluate.Count == 0)
        {
            providersToEvaluate = _structures.Values.ToList();
        }

        var calculatedFees = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in providersToEvaluate)
        {
            var fee = Math.Round((amount * (s.PercentageFee / 100m)) + s.FixedFee, 2);
            calculatedFees[s.ProviderName] = fee;
        }

        var best = calculatedFees.OrderBy(kvp => kvp.Value).First();

        return new KyrolusFeeOptimizationResult
        {
            RecommendedProviderName = best.Key,
            EstimatedFee = best.Value,
            NetMerchantAmount = amount - best.Value,
            AllProviderFees = calculatedFees
        };
    }
}
