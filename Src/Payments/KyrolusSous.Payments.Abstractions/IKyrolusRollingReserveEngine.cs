namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusRollingReserveEngine
{
    KyrolusReserveHoldEntry ApplyReserveHold(
        string merchantId,
        string transactionId,
        decimal transactionGrossAmount,
        string currency,
        decimal holdPercentage = 5.0m,
        TimeSpan? holdDuration = null);

    decimal ReleaseEligibleHolds(string merchantId, DateTimeOffset asOfUtc);
    KyrolusReserveStatusSummary GetReserveSummary(string merchantId, string currency = "USD");
}
