using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultRefundPolicyEngine : IKyrolusRefundPolicyEngine
{
    public KyrolusRefundCalculationResult CalculateRefund(KyrolusRefundCalculationRequest request)
    {
        var ageDays = (DateTimeOffset.UtcNow - request.OrderCompletedAtUtc).TotalDays;

        if (ageDays > request.AllowedRefundWindowDays)
        {
            return new KyrolusRefundCalculationResult
            {
                IsEligibleForRefund = false,
                GrossRefundAmount = request.RequestedRefundAmount,
                RestockingFeeDeduction = 0m,
                NonRefundableShippingDeduction = 0m,
                NetApprovedRefundAmount = 0m,
                IneligibilityReason = $"Refund window expired. Order was completed {Math.Round(ageDays)} days ago (allowed window: {request.AllowedRefundWindowDays} days)."
            };
        }

        var restockingFee = Math.Round(request.RequestedRefundAmount * (request.RestockingFeePercent / 100m), 2);
        var nonRefundableShipping = request.IsShippingRefundable ? 0m : request.OriginalShippingCost;

        var netRefund = Math.Max(0m, request.RequestedRefundAmount - restockingFee - nonRefundableShipping);

        return new KyrolusRefundCalculationResult
        {
            IsEligibleForRefund = true,
            GrossRefundAmount = request.RequestedRefundAmount,
            RestockingFeeDeduction = restockingFee,
            NonRefundableShippingDeduction = nonRefundableShipping,
            NetApprovedRefundAmount = netRefund
        };
    }
}
