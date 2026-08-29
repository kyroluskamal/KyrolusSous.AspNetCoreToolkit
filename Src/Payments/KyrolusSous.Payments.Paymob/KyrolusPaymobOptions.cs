namespace KyrolusSous.Payments.Paymob;

public sealed class KyrolusPaymobOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string HmacSecret { get; set; } = string.Empty;
    public int IntegrationId { get; set; }
    public int IframeId { get; set; }
    public string BaseUrl { get; set; } = "https://accept.paymob.com/api";
}
