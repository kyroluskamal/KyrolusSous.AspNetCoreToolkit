namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool IsSandbox { get; set; } = true;
    public string? WebhookId { get; set; }

    public string BaseUrl => IsSandbox
        ? "https://api-m.sandbox.paypal.com"
        : "https://api-m.paypal.com";
}
