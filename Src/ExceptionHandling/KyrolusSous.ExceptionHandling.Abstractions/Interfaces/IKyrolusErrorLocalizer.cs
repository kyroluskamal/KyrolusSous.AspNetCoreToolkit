namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

/// <summary>
/// Defines the contract for localizing error messages and titles into different languages/cultures.
/// </summary>
public interface IKyrolusErrorLocalizer
{
    /// <summary>
    /// Translates an error code into a localized message for the requested culture.
    /// </summary>
    /// <param name="code">The unique error code identifier.</param>
    /// <param name="defaultMessage">The fallback default message if no translation exists.</param>
    /// <param name="culture">The target culture for localization.</param>
    /// <returns>The localized message string, or the default message if not found.</returns>
    string? Localize(string code, string? defaultMessage, CultureInfo? culture);
}
