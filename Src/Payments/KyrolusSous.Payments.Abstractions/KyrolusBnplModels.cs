namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusInstallmentOption
{
    public required int InstallmentMonths { get; init; } // e.g. 3, 6, 12
    public required decimal MonthlyAmount { get; init; }
    public required decimal DownPaymentAmount { get; init; }
    public required decimal TotalPayableAmount { get; init; }
    public decimal AdminFeeAmount { get; init; } = 0m;
    public decimal InterestRatePercent { get; init; } = 0m;
}

public sealed record KyrolusBnplCalculationResult
{
    public required decimal OrderAmount { get; init; }
    public required string Currency { get; init; }
    public IReadOnlyList<KyrolusInstallmentOption> AvailablePlans { get; init; } = [];
}
