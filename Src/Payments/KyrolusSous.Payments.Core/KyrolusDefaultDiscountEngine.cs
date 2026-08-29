using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultDiscountEngine : IKyrolusDiscountEngine
{
    private readonly ConcurrentDictionary<string, KyrolusCoupon> _coupons = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterCoupon(KyrolusCoupon coupon)
    {
        _coupons[coupon.Code] = coupon;
    }

    public KyrolusApplyDiscountResult CalculateDiscount(KyrolusApplyDiscountRequest request)
    {
        if (!_coupons.TryGetValue(request.CouponCode, out var coupon) || !coupon.IsActive)
        {
            return new KyrolusApplyDiscountResult
            {
                IsValid = false,
                CouponCode = request.CouponCode,
                OriginalAmount = request.OrderAmount,
                DiscountAmount = 0m,
                FinalAmount = request.OrderAmount,
                ErrorMessage = "Invalid or inactive coupon code."
            };
        }

        if (coupon.ExpiresAtUtc.HasValue && DateTimeOffset.UtcNow > coupon.ExpiresAtUtc.Value)
        {
            return new KyrolusApplyDiscountResult
            {
                IsValid = false,
                CouponCode = request.CouponCode,
                OriginalAmount = request.OrderAmount,
                DiscountAmount = 0m,
                FinalAmount = request.OrderAmount,
                ErrorMessage = "Coupon has expired."
            };
        }

        if (coupon.MinimumOrderAmount.HasValue && request.OrderAmount < coupon.MinimumOrderAmount.Value)
        {
            return new KyrolusApplyDiscountResult
            {
                IsValid = false,
                CouponCode = request.CouponCode,
                OriginalAmount = request.OrderAmount,
                DiscountAmount = 0m,
                FinalAmount = request.OrderAmount,
                ErrorMessage = $"Order amount must be at least {coupon.MinimumOrderAmount.Value} to use this coupon."
            };
        }

        if (coupon.MaxUsageCount.HasValue && coupon.CurrentUsageCount >= coupon.MaxUsageCount.Value)
        {
            return new KyrolusApplyDiscountResult
            {
                IsValid = false,
                CouponCode = request.CouponCode,
                OriginalAmount = request.OrderAmount,
                DiscountAmount = 0m,
                FinalAmount = request.OrderAmount,
                ErrorMessage = "Coupon usage limit has been reached."
            };
        }

        decimal discount = coupon.Type == KyrolusDiscountType.Percentage
            ? request.OrderAmount * (coupon.Value / 100m)
            : coupon.Value;

        if (coupon.MaximumDiscountAmount.HasValue && discount > coupon.MaximumDiscountAmount.Value)
        {
            discount = coupon.MaximumDiscountAmount.Value;
        }

        discount = Math.Min(discount, request.OrderAmount);
        var final = request.OrderAmount - discount;

        return new KyrolusApplyDiscountResult
        {
            IsValid = true,
            CouponCode = request.CouponCode,
            OriginalAmount = request.OrderAmount,
            DiscountAmount = discount,
            FinalAmount = final
        };
    }
}
