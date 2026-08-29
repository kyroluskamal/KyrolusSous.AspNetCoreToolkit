namespace KyrolusSous.Payments.Square;

public sealed class KyrolusSquareOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public bool IsSandbox { get; set; } = true;

    public string BaseUrl => IsSandbox
        ? "https://connect.squareupsandbox.com/v2"
        : "https://connect.squareup.com/v2";
}
