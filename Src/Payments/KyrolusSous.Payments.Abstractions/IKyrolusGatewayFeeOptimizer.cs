namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusGatewayFeeOptimizer
{
    void RegisterFeeStructure(KyrolusProviderFeeStructure structure);
    KyrolusFeeOptimizationResult OptimizeFee(decimal amount, string currency, IReadOnlyList<string>? candidateProviders = null);
}
