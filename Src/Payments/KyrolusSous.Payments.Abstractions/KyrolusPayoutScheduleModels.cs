namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusSettlementSpeed
{
    T_Plus_0_Instant,
    T_Plus_1_NextDay,
    T_Plus_2_Standard,
    T_Plus_7_Weekly
}

public sealed record KyrolusPayoutScheduleRequest
{
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required KyrolusSettlementSpeed Speed { get; init; }
    public required string BankCountryCode { get; init; } // e.g. "EG", "US", "GB"
}

public sealed record KyrolusPayoutScheduleResult
{
    public required DateTimeOffset EstimatedPayoutArrivalDateUtc { get; init; }
    public required int BusinessDaysAdded { get; init; }
    public required int WeekendAndHolidayDaysDelayed { get; init; }
    public required bool IsInstantSettlement { get; init; }
}
