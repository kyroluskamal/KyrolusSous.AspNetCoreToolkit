namespace KyrolusSous.Payments.Stripe;

public sealed class KyrolusStripeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string? WebhookSecret { get; set; }
    public string BaseUrl { get; set; } = "https://api.stripe.com/v1/";
}
