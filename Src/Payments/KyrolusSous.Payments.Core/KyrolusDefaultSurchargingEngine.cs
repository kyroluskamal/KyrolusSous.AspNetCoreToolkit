using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultSurchargingEngine : IKyrolusSurchargingEngine
{
    public KyrolusSurchargeEvaluationResult CalculateCompliantSurcharge(KyrolusSurchargeEvaluationRequest request)
    {
        var country = request.CountryCode.Trim().ToUpperInvariant();

        // UK and EU ban surcharging entirely (PSD2 / Consumer Rights (Payment Surcharges) Regulations)
        if (country is "GB" or "UK" or "DE" or "FR" or "ES" or "IT" or "NL")
        {
            return new KyrolusSurchargeEvaluationResult
            {
                OriginalAmount = request.OrderAmount,
                AllowedSurchargeRatePercent = 0m,
                SurchargeAmount = 0m,
                FinalCustomerChargeAmount = request.OrderAmount,
                IsSurchargePermitted = false,
                ComplianceNote = "Surcharging on consumer cards is strictly prohibited under UK/EU PSD2 Regulations."
            };
        }

        // Debit cards cannot be surcharged under US Durbin amendment
        if (request.CardType == KyrolusCardType.Debit)
        {
            return new KyrolusSurchargeEvaluationResult
            {
                OriginalAmount = request.OrderAmount,
                AllowedSurchargeRatePercent = 0m,
                SurchargeAmount = 0m,
                FinalCustomerChargeAmount = request.OrderAmount,
                IsSurchargePermitted = false,
                ComplianceNote = "Debit cards cannot be surcharged under US Durbin Amendment / card network rules."
            };
        }

        // US credit card surcharge cap (maximum 3.0% under card brand rules)
        var allowedRate = Math.Min(3.0m, request.RequestedSurchargePercent);
        var surcharge = Math.Round(request.OrderAmount * (allowedRate / 100m), 2);

        return new KyrolusSurchargeEvaluationResult
        {
            OriginalAmount = request.OrderAmount,
            AllowedSurchargeRatePercent = allowedRate,
            SurchargeAmount = surcharge,
            FinalCustomerChargeAmount = request.OrderAmount + surcharge,
            IsSurchargePermitted = true,
            ComplianceNote = "Compliant credit card surcharge applied within statutory caps."
        };
    }
}
