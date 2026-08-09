namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

public interface IKyrolusErrorLocalizer
{
    string? Localize(string code, string? defaultMessage, CultureInfo? culture);
}
