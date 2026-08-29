namespace KyrolusSous.Payments.Fawry;

public sealed class KyrolusFawryOptions
{
    public string MerchantCode { get; set; } = string.Empty;
    public string SecurityKey { get; set; } = string.Empty;
    public bool IsSandbox { get; set; } = true;

    public string BaseUrl => IsSandbox
        ? "https://atfawry.fawrystaging.com"
        : "https://www.atfawry.com";
}
