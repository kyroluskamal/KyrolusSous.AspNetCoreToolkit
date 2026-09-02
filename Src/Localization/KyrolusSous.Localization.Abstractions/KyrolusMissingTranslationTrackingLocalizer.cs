namespace KyrolusSous.Localization.Abstractions;

/// <summary>Observes a lookup that failed to resolve a translation - the key and the culture it was requested for.</summary>
public delegate void KyrolusMissingTranslationHandler(string key, CultureInfo culture);

/// <summary>
/// Wraps an <see cref="IKyrolusLocalizer"/> and invokes <paramref name="onMissing"/> whenever a lookup reports
/// <see cref="KyrolusLocalizationResult.ResourceNotFound"/>, so missing keys can be logged/counted/alerted on
/// in production instead of only being noticed when a user reports oddly-untranslated text. Works around any
/// underlying localizer (JSON, dictionary, an <c>IStringLocalizer</c> adapter, or even a
/// <see cref="KyrolusCompositeLocalizer"/>) without that localizer needing to know about tracking at all.
/// </summary>
/// <param name="inner">The localizer to wrap.</param>
/// <param name="onMissing">Invoked with the key and resolved culture whenever a lookup does not find a translation.</param>
/// <example>
/// <code>
/// services.AddSingleton&lt;IKyrolusLocalizer&gt;(sp =&gt; new KyrolusMissingTranslationTrackingLocalizer(
///     new KyrolusJsonLocalizer(options),
///     (key, culture) =&gt; logger.LogWarning("Missing translation {Key} for {Culture}", key, culture)));
/// </code>
/// </example>
public sealed class KyrolusMissingTranslationTrackingLocalizer(IKyrolusLocalizer inner, KyrolusMissingTranslationHandler onMissing) : IKyrolusLocalizer
{
    private readonly IKyrolusLocalizer _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly KyrolusMissingTranslationHandler _onMissing = onMissing ?? throw new ArgumentNullException(nameof(onMissing));

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
    {
        var result = _inner.GetString(key, culture);
        if (result.ResourceNotFound)
            _onMissing(key, culture ?? CultureInfo.CurrentUICulture);

        return result;
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
    {
        var result = _inner.GetString(key, arguments, culture);
        if (result.ResourceNotFound)
            _onMissing(key, culture ?? CultureInfo.CurrentUICulture);

        return result;
    }

    /// <inheritdoc />
    public string Format(string template, object? arguments) => _inner.Format(template, arguments);

    /// <inheritdoc />
    public IEnumerable<string> GetAllKeys(CultureInfo? culture = null) => _inner.GetAllKeys(culture);

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableCultures() => _inner.GetAvailableCultures();
}
