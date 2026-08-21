
namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusNullErrorLocalizer : IKyrolusErrorLocalizer
{
    public string? Localize(string code, string? defaultMessage, CultureInfo? culture)
    => defaultMessage;
}
