using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultThreeDSecureEngine : IKyrolusThreeDSecureEngine
{
    public KyrolusThreeDSecureEvaluationResult EvaluateRiskAndFlow(KyrolusThreeDSecureEvaluationRequest request)
    {
        // 1. Low Value Exemption (< $30 or < 30 EUR)
        if (request.Amount < 30m && (request.Currency.Equals("EUR", StringComparison.OrdinalIgnoreCase) || request.Currency.Equals("USD", StringComparison.OrdinalIgnoreCase)))
        {
            return new KyrolusThreeDSecureEvaluationResult
            {
                RecommendedFlow = KyrolusThreeDSecureFlow.ExemptedLowValue,
                EciFlag = "06",
                ExemptionReason = "SCA Exemption: Low Value Transaction (< 30 EUR/USD)"
            };
        }

        // 2. Trusted Beneficiary Exemption
        if (request.IsTrustedBeneficiary || request.IsRecurringTransaction)
        {
            return new KyrolusThreeDSecureEvaluationResult
            {
                RecommendedFlow = KyrolusThreeDSecureFlow.ExemptedTrustedMerchant,
                EciFlag = "06",
                ExemptionReason = "SCA Exemption: Merchant Initiated / Whitelisted Beneficiary"
            };
        }

        // 3. Frictionless flow for standard amounts
        if (request.Amount <= 250m)
        {
            return new KyrolusThreeDSecureEvaluationResult
            {
                RecommendedFlow = KyrolusThreeDSecureFlow.Frictionless,
                EciFlag = "05"
            };
        }

        // 4. High-value / high-risk -> Challenge Required (OTP)
        return new KyrolusThreeDSecureEvaluationResult
        {
            RecommendedFlow = KyrolusThreeDSecureFlow.ChallengeRequired,
            EciFlag = "07",
            ChallengeUrl = $"https://acs.kyrolussous.com/3ds2/challenge?tx={Guid.NewGuid():N}"
        };
    }
}
