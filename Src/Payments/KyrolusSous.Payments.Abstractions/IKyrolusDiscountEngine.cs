namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusDiscountEngine
{
    void RegisterCoupon(KyrolusCoupon coupon);
    KyrolusApplyDiscountResult CalculateDiscount(KyrolusApplyDiscountRequest request);
}
