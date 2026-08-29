namespace KyrolusSous.Payments.Adyen;

public sealed class KyrolusAdyenOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string MerchantAccount { get; set; } = string.Empty;
    public string HmacKey { get; set; } = string.Empty;
    public bool IsLive { get; set; } = false;

    public string BaseUrl => IsLive
        ? "https://checkout-live.adyen.com/v70"
        : "https://checkout-test.adyen.com/v70";
}
