namespace KyrolusSous.ExceptionHandling.Abstractions.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IKyrolusErrorLocalizer"/> to simplify envelope and error message localization.
/// </summary>
public static class KyrolusErrorLocalizerExtensions
{
    /// <summary>
    /// Localizes the title and detail of a <see cref="KyrolusErrorEnvelope"/> according to the target culture.
    /// </summary>
    /// <param name="localizer">The error localizer instance.</param>
    /// <param name="envelope">The envelope to localize.</param>
    /// <param name="culture">The target culture.</param>
    /// <returns>A localized copy of the envelope, or the original envelope if localizer is null.</returns>
    public static KyrolusErrorEnvelope Localize(
        this IKyrolusErrorLocalizer? localizer,
        KyrolusErrorEnvelope envelope,
        CultureInfo? culture)
    {
        if (localizer is null)
            return envelope;

        var title = localizer.Localize(envelope.Code, envelope.Title, culture) ?? envelope.Title;
        var detail = localizer.Localize($"{envelope.Code}.detail", envelope.Detail, culture) ?? envelope.Detail;

        return envelope with { Title = title, Detail = detail };
    }

    /// <summary>
    /// Localizes error title and detail strings for a specific error code.
    /// </summary>
    /// <param name="localizer">The error localizer instance.</param>
    /// <param name="code">The unique error code.</param>
    /// <param name="defaultTitle">Fallback title string.</param>
    /// <param name="defaultDetail">Fallback detail string.</param>
    /// <param name="culture">The target culture.</param>
    /// <returns>A tuple containing the localized title and detail.</returns>
    public static (string Title, string? Detail) Localize(
        this IKyrolusErrorLocalizer? localizer,
        string code,
        string defaultTitle,
        string? defaultDetail,
        CultureInfo? culture)
    {
        var env = Localize(localizer, new KyrolusErrorEnvelope(code, defaultTitle, defaultDetail), culture);

        return (env.Title, env.Detail);
    }
}
