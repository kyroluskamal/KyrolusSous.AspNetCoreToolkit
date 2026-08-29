using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultBnplInstallmentCalculator : IKyrolusBnplInstallmentCalculator
{
    public KyrolusBnplCalculationResult CalculatePlans(decimal orderAmount, string currency = "EGP")
    {
        var plans = new List<KyrolusInstallmentOption>
        {
            // Plan 1: 3 months (0% interest, 0% admin fee, 25% down payment)
            new()
            {
                InstallmentMonths = 3,
                DownPaymentAmount = Math.Round(orderAmount * 0.25m, 2),
                MonthlyAmount = Math.Round((orderAmount * 0.75m) / 3m, 2),
                TotalPayableAmount = orderAmount,
                InterestRatePercent = 0m,
                AdminFeeAmount = 0m
            },
            // Plan 2: 6 months (5% interest, 0 down payment)
            new()
            {
                InstallmentMonths = 6,
                DownPaymentAmount = 0m,
                MonthlyAmount = Math.Round((orderAmount * 1.05m) / 6m, 2),
                TotalPayableAmount = Math.Round(orderAmount * 1.05m, 2),
                InterestRatePercent = 5m,
                AdminFeeAmount = 0m
            },
            // Plan 3: 12 months (10% interest + $5 admin fee, 0 down payment)
            new()
            {
                InstallmentMonths = 12,
                DownPaymentAmount = 0m,
                MonthlyAmount = Math.Round(((orderAmount * 1.10m) + 5m) / 12m, 2),
                TotalPayableAmount = Math.Round((orderAmount * 1.10m) + 5m, 2),
                InterestRatePercent = 10m,
                AdminFeeAmount = 5m
            }
        };

        return new KyrolusBnplCalculationResult
        {
            OrderAmount = orderAmount,
            Currency = currency,
            AvailablePlans = plans.AsReadOnly()
        };
    }
}
