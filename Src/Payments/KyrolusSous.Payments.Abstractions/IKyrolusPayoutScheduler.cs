namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPayoutScheduler
{
    KyrolusPayoutScheduleResult CalculateExpectedPayoutDate(KyrolusPayoutScheduleRequest request);
}
