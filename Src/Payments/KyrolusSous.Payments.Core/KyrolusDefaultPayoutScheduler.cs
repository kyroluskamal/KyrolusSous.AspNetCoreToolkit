using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultPayoutScheduler : IKyrolusPayoutScheduler
{
    public KyrolusPayoutScheduleResult CalculateExpectedPayoutDate(KyrolusPayoutScheduleRequest request)
    {
        if (request.Speed == KyrolusSettlementSpeed.T_Plus_0_Instant)
        {
            return new KyrolusPayoutScheduleResult
            {
                EstimatedPayoutArrivalDateUtc = request.CapturedAtUtc,
                BusinessDaysAdded = 0,
                WeekendAndHolidayDaysDelayed = 0,
                IsInstantSettlement = true
            };
        }

        int targetBusinessDays = request.Speed switch
        {
            KyrolusSettlementSpeed.T_Plus_1_NextDay => 1,
            KyrolusSettlementSpeed.T_Plus_2_Standard => 2,
            KyrolusSettlementSpeed.T_Plus_7_Weekly => 7,
            _ => 2
        };

        var current = request.CapturedAtUtc;
        int businessDaysCounted = 0;
        int delayedDays = 0;
        var isEgypt = request.BankCountryCode.Equals("EG", StringComparison.OrdinalIgnoreCase);

        while (businessDaysCounted < targetBusinessDays)
        {
            current = current.AddDays(1);
            var isWeekend = isEgypt
                ? current.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday
                : current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            if (isWeekend)
            {
                delayedDays++;
            }
            else
            {
                businessDaysCounted++;
            }
        }

        return new KyrolusPayoutScheduleResult
        {
            EstimatedPayoutArrivalDateUtc = current,
            BusinessDaysAdded = targetBusinessDays,
            WeekendAndHolidayDaysDelayed = delayedDays,
            IsInstantSettlement = false
        };
    }
}
