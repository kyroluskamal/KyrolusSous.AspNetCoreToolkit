namespace KyrolusSous.Payments.Tap;

public sealed class KyrolusTapOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.tap.company/v2";
}
