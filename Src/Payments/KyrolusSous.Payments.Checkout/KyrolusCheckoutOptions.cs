namespace KyrolusSous.Payments.Checkout;

public sealed class KyrolusCheckoutOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public bool IsSandbox { get; set; } = true;

    public string BaseUrl => IsSandbox
        ? "https://api.sandbox.checkout.com"
        : "https://api.checkout.com";
}
