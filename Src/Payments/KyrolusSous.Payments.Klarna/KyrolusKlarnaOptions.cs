namespace KyrolusSous.Payments.Klarna;

public sealed class KyrolusKlarnaOptions
{
    public string ApiUsername { get; set; } = string.Empty;
    public string ApiPassword { get; set; } = string.Empty;
    public bool IsLive { get; set; } = false;

    public string BaseUrl => IsLive
        ? "https://api.klarna.com"
        : "https://api.playground.klarna.com";
}
