namespace KyrolusSous.Payments.Mollie;

public sealed class KyrolusMollieOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.mollie.com/v2";
}
