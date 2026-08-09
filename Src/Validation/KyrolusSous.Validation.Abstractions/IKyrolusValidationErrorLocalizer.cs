namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusValidationErrorLocalizer
{
    string Localize(KyrolusValidationFailure failure, System.Globalization.CultureInfo? culture = null);
}
