using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Payments.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusPayments(this IServiceCollection services, bool registerMockProvider = true)
    {
        if (registerMockProvider)
        {
            services.AddSingleton<IKyrolusPaymentProvider, KyrolusMockPaymentProvider>();
            services.AddSingleton<IKyrolusWebhookHandler, KyrolusMockWebhookHandler>();
            services.AddSingleton<IKyrolusSubscriptionProvider, KyrolusMockSubscriptionProvider>();
            services.AddSingleton<IKyrolusCustomerVaultProvider, KyrolusMockCustomerVaultProvider>();
            services.AddSingleton<IKyrolusPaymentLinkProvider, KyrolusMockPaymentLinkProvider>();
            services.AddSingleton<IKyrolusMarketplaceProvider, KyrolusMockMarketplaceProvider>();
            services.AddSingleton<IKyrolusDisputeProvider, KyrolusMockDisputeProvider>();
            services.AddSingleton<IKyrolusPayoutProvider, KyrolusMockPayoutProvider>();
            services.AddSingleton<IKyrolusEscrowProvider, KyrolusMockEscrowProvider>();
            services.AddSingleton<IKyrolusVirtualCardProvider, KyrolusMockVirtualCardProvider>();
            services.AddSingleton<IKyrolusCryptoPaymentProvider, KyrolusMockCryptoPaymentProvider>();
        }

        services.TryAddSingleton<IKyrolusPaymentIdempotencyStore, KyrolusCachePaymentIdempotencyStore>();
        services.TryAddSingleton<IKyrolusSmartPaymentRouter, KyrolusSmartPaymentRouter>();
        services.TryAddSingleton<IKyrolusFraudDetectionEngine, KyrolusDefaultFraudDetectionEngine>();
        services.TryAddSingleton<IKyrolusDunningEngine, KyrolusDefaultDunningEngine>();
        services.TryAddSingleton<IKyrolusInvoiceGenerator, KyrolusDefaultInvoiceGenerator>();
        services.TryAddSingleton<IKyrolusWebhookReplayProtector, KyrolusDefaultWebhookReplayProtector>();
        services.TryAddSingleton<IKyrolusPaymentMetricsCollector, KyrolusDefaultPaymentMetricsCollector>();
        services.TryAddSingleton<IKyrolusDiscountEngine, KyrolusDefaultDiscountEngine>();
        services.TryAddSingleton<IKyrolusSplitTenderProvider, KyrolusDefaultSplitTenderProvider>();
        services.TryAddSingleton<IKyrolusFxRateProvider, KyrolusDefaultFxRateProvider>();
        services.TryAddSingleton<IKyrolusReconciliationEngine, KyrolusDefaultReconciliationEngine>();
        services.TryAddSingleton<IKyrolusOfflinePaymentSyncEngine, KyrolusDefaultOfflinePaymentSyncEngine>();
        services.TryAddSingleton<IKyrolusBinLookupProvider, KyrolusDefaultBinLookupProvider>();
        services.TryAddSingleton<IKyrolusGatewayFeeOptimizer, KyrolusDefaultGatewayFeeOptimizer>();
        services.TryAddSingleton<IKyrolusApplePayDecryptor, KyrolusDefaultApplePayDecryptor>();
        services.TryAddSingleton<IKyrolusMeteredBillingEngine, KyrolusDefaultMeteredBillingEngine>();
        services.TryAddSingleton<IKyrolusLoyaltyRewardsEngine, KyrolusDefaultLoyaltyRewardsEngine>();
        services.TryAddSingleton<IKyrolusCardAccountUpdater, KyrolusDefaultCardAccountUpdater>();
        services.TryAddSingleton<IKyrolusTaxCalculationEngine, KyrolusDefaultTaxCalculationEngine>();
        services.TryAddSingleton<IKyrolusBnplInstallmentCalculator, KyrolusDefaultBnplInstallmentCalculator>();
        services.TryAddSingleton<IKyrolusThreeDSecureEngine, KyrolusDefaultThreeDSecureEngine>();
        services.TryAddSingleton<IKyrolusChargebackDefenseEngine, KyrolusDefaultChargebackDefenseEngine>();
        services.TryAddSingleton<IKyrolusSurchargingEngine, KyrolusDefaultSurchargingEngine>();
        services.TryAddSingleton<IKyrolusConditionalReleaseEngine, KyrolusDefaultConditionalReleaseEngine>();
        services.TryAddSingleton<IKyrolusSettlementRouteOptimizer, KyrolusDefaultSettlementRouteOptimizer>();
        services.TryAddSingleton<IKyrolusDirectDebitMandateEngine, KyrolusDefaultDirectDebitMandateEngine>();
        services.TryAddSingleton<IKyrolusNetworkTokenizationEngine, KyrolusDefaultNetworkTokenizationEngine>();
        services.TryAddSingleton<IKyrolusDynamicCurrencyConversionEngine, KyrolusDefaultDynamicCurrencyConversionEngine>();
        services.TryAddSingleton<IKyrolusGiftCardPassEngine, KyrolusDefaultGiftCardPassEngine>();
        services.TryAddSingleton<IKyrolusRollingReserveEngine, KyrolusDefaultRollingReserveEngine>();
        services.TryAddSingleton<IKyrolusPaymentWebhookDispatcher, KyrolusDefaultPaymentWebhookDispatcher>();
        services.TryAddSingleton<IKyrolusMerchantKycEngine, KyrolusDefaultMerchantKycEngine>();
        services.TryAddSingleton<IKyrolusInterchangePlusCalculator, KyrolusDefaultInterchangePlusCalculator>();
        services.TryAddSingleton<IKyrolusRefundPolicyEngine, KyrolusDefaultRefundPolicyEngine>();
        services.TryAddSingleton<IKyrolusPayoutScheduler, KyrolusDefaultPayoutScheduler>();

        services.AddSingleton<IKyrolusPaymentFactory, KyrolusPaymentFactory>();
        return services;
    }
}
