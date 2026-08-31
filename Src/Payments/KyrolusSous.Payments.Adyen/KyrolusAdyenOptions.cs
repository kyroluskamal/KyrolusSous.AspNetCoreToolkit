namespace KyrolusSous.Payments.Adyen;

public sealed class KyrolusAdyenOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string MerchantAccount { get; set; } = string.Empty;
    public string HmacKey { get; set; } = string.Empty;
    public bool IsLive { get; set; } = false;

    /// <summary>
    /// Currency to use when capturing a payment via the (transactionId, amount) overload, which
    /// isn't told the original authorization's currency. Only correct if all payments processed
    /// through this provider use the same currency; otherwise capture with the full authorized
    /// amount (no partial amount) so Adyen captures in the original currency automatically.
    /// </summary>
    public string DefaultCaptureCurrency { get; set; } = "EUR";

    public string BaseUrl => IsLive
        ? "https://checkout-live.adyen.com/v70"
        : "https://checkout-test.adyen.com/v70";
}
