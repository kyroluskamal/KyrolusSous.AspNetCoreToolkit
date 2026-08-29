using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultInterchangePlusCalculator : IKyrolusInterchangePlusCalculator
{
    public KyrolusInterchangeFeeBreakdown CalculateFeeBreakdown(KyrolusInterchangePricingRequest request)
    {
        // 1. Interchange Rate (Paid to issuing bank)
        decimal interchangeRate = (request.CardType, request.Scheme) switch
        {
            (KyrolusCardType.Debit, KyrolusCardScheme.Meeza) => 0.005m, // 0.50%
            (KyrolusCardType.Debit, _) => 0.008m, // 0.80% (regulated debit)
            (KyrolusCardType.Credit, _) => 0.0165m, // 1.65%
            _ => 0.015m
        };

        if (request.IsCrossBorder) interchangeRate += 0.008m; // Cross-border markup

        var interchangeFee = Math.Round(request.TransactionAmount * interchangeRate, 2);

        // 2. Scheme Assessment Fee (Visa / Mastercard network fee)
        decimal schemeRate = 0.0014m; // 0.14% standard scheme assessment
        var schemeFee = Math.Round(request.TransactionAmount * schemeRate, 2);

        // 3. Acquirer Markup
        var markupFee = Math.Round(request.TransactionAmount * (request.AcquirerMarkupPercent / 100m) + request.AcquirerFixedFee, 2);

        var totalCost = interchangeFee + schemeFee + markupFee;
        var netSettlement = request.TransactionAmount - totalCost;
        var effectiveRate = request.TransactionAmount > 0
            ? Math.Round((totalCost / request.TransactionAmount) * 100m, 2)
            : 0m;

        return new KyrolusInterchangeFeeBreakdown
        {
            TransactionAmount = request.TransactionAmount,
            InterchangeFee = interchangeFee,
            SchemeAssessmentFee = schemeFee,
            AcquirerMarkupFee = markupFee,
            TotalProcessingCost = totalCost,
            NetSettlementAmount = netSettlement,
            EffectiveRatePercent = effectiveRate
        };
    }
}
