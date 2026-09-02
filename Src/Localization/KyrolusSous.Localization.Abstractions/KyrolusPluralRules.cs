using System.Collections.Concurrent;

namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// A CLDR plural category, used to select which "key.category" variant of a translation applies to a given count.
/// </summary>
public enum KyrolusPluralCategory
{
    /// <summary>Applies to a count of exactly zero, in languages that give zero its own form (e.g. Arabic).</summary>
    Zero,

    /// <summary>Applies to a count that behaves grammatically like "one" (usually, but not always, exactly 1).</summary>
    One,

    /// <summary>Applies to a count of exactly two, in languages with a dual form (e.g. Arabic).</summary>
    Two,

    /// <summary>Applies to a small-count range in languages that distinguish it from "many" (e.g. Arabic, Slavic languages).</summary>
    Few,

    /// <summary>Applies to a larger-count range in languages that distinguish it from "few" (e.g. Arabic, Slavic languages).</summary>
    Many,

    /// <summary>The catch-all category every language must define; used when no more specific category applies.</summary>
    Other
}

/// <summary>Computes the plural category for a non-negative magnitude, under one specific language's rule.</summary>
/// <param name="n">The count's magnitude (sign already stripped, <see cref="long.MinValue"/> already clamped).</param>
public delegate KyrolusPluralCategory KyrolusPluralRule(long n);

/// <summary>
/// Resolves the CLDR plural category for a culture/count pair via a registry of per-language rules, so any
/// language can be supported with its own correct rule instead of being limited to whatever this library ships
/// with - .NET's base class library has no built-in CLDR/ICU plural-rules API to draw on, so this is a small,
/// deliberately pluggable registry rather than an attempt to hand-code every CLDR locale. Built in:
/// <list type="bullet">
/// <item><description>Arabic ("ar") - all six categories.</description></item>
/// <item><description>The common Slavic family ("ru", "uk", "be", "sr", "hr", "bs").</description></item>
/// <item><description>French ("fr") - 0 and 1 both count as "one".</description></item>
/// <item><description>Portuguese ("pt") - 0 and 1 both count as "one", <b>except</b> Brazilian Portuguese ("pt-BR"), registered separately since CLDR gives it its own rule where only 1 is "one" - a real-world example of why an exact culture name (see <see cref="Register"/>) can need to override its own language's rule.</description></item>
/// <item><description>German ("de"), Swedish ("sv"), and Spanish ("es") - explicitly registered for clarity even though each happens to use the same "one" (n == 1) / "other" split as the default below, so their correctness doesn't silently depend on that default never changing.</description></item>
/// </list>
/// Any language without a registered rule falls back to the English-like "one" (n == 1) / "other" (everything
/// else) split, which happens to be correct for most Germanic, Romance, and East-Asian languages anyway. Call
/// <see cref="Register"/> at startup to add a language this library doesn't cover (Polish, Hebrew, Welsh, ...)
/// or to override a built-in rule with your own.
/// </summary>
/// <example>
/// <code>
/// // Polish: one = n==1; few = n%10 in 2..4 and n%100 not in 12..14; many = everything else (n != 1).
/// KyrolusPluralRules.Register("pl", n =>
/// {
///     if (n == 1) return KyrolusPluralCategory.One;
///     var mod10 = n % 10;
///     var mod100 = n % 100;
///     if (mod10 is >= 2 and &lt;= 4 &amp;&amp; mod100 is &lt; 12 or &gt; 14) return KyrolusPluralCategory.Few;
///     return KyrolusPluralCategory.Many;
/// });
/// </code>
/// </example>
public static class KyrolusPluralRules
{
    private static readonly ConcurrentDictionary<string, KyrolusPluralRule> Rules = BuildDefaults();

    /// <summary>
    /// Registers (or overrides) the plural rule for a language or culture name. Registering under a two-letter
    /// language code (e.g. "pl") applies to every culture of that language; registering under a full culture
    /// name (e.g. "pt-BR") applies only to that specific culture and takes precedence over a language-level
    /// rule for the same language when both are registered.
    /// </summary>
    /// <param name="languageOrCultureName">A two-letter language code (e.g. "ar") or a full culture name (e.g. "pt-BR").</param>
    /// <param name="rule">Computes the plural category for a given (already sign-stripped) count magnitude.</param>
    public static void Register(string languageOrCultureName, KyrolusPluralRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageOrCultureName);
        ArgumentNullException.ThrowIfNull(rule);
        Rules[languageOrCultureName] = rule;
    }

    /// <summary>
    /// Resolves the plural category for <paramref name="count"/> under <paramref name="culture"/>'s registered
    /// rule - the exact culture name (e.g. "pt-BR") if one is registered, else the two-letter language code
    /// (e.g. "pt"), else the English-like "one"/"other" default.
    /// </summary>
    /// <param name="culture">The culture whose plural rule should apply.</param>
    /// <param name="count">The count being pluralized for. The sign is ignored (rules operate on magnitude).</param>
    public static KyrolusPluralCategory Resolve(CultureInfo culture, long count)
    {
        ArgumentNullException.ThrowIfNull(culture);

        // long.MinValue has no positive counterpart representable as a long (two's complement asymmetry), so
        // Math.Abs would throw for it; treat it as the largest magnitude instead of guessing further.
        var n = count == long.MinValue ? long.MaxValue : Math.Abs(count);

        if (!string.IsNullOrEmpty(culture.Name) && Rules.TryGetValue(culture.Name, out var byCulture))
            return byCulture(n);

        if (!string.IsNullOrEmpty(culture.TwoLetterISOLanguageName) && Rules.TryGetValue(culture.TwoLetterISOLanguageName, out var byLanguage))
            return byLanguage(n);

        return n == 1 ? KyrolusPluralCategory.One : KyrolusPluralCategory.Other;
    }

    private static ConcurrentDictionary<string, KyrolusPluralRule> BuildDefaults()
    {
        var zeroAndOneAreOne = new KyrolusPluralRule(n => n is 0 or 1 ? KyrolusPluralCategory.One : KyrolusPluralCategory.Other);
        var onlyOneIsOne = new KyrolusPluralRule(n => n == 1 ? KyrolusPluralCategory.One : KyrolusPluralCategory.Other);

        var rules = new ConcurrentDictionary<string, KyrolusPluralRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["ar"] = ResolveArabic,
            ["fr"] = zeroAndOneAreOne,

            // CLDR gives European/generic Portuguese "0 and 1 are one", but Brazilian Portuguese its own rule
            // where only 1 is "one" - registering "pt-BR" as an exact culture name overrides the "pt"
            // language-level rule for that one culture only (see Resolve's precedence order), while every
            // other Portuguese culture (pt-PT, plain "pt", ...) keeps the "pt" rule.
            ["pt"] = zeroAndOneAreOne,
            ["pt-BR"] = onlyOneIsOne,

            // Each of these happens to match the English-like default Resolve() falls back to anyway; they're
            // registered explicitly so their correctness doesn't silently depend on that default never changing.
            ["de"] = onlyOneIsOne,
            ["sv"] = onlyOneIsOne,
            ["es"] = onlyOneIsOne
        };

        foreach (var slavicLanguage in new[] { "ru", "uk", "be", "sr", "hr", "bs" })
            rules[slavicLanguage] = ResolveSlavic;

        return rules;
    }

    /// <summary>CLDR Arabic rule: zero=0, one=1, two=2, few=n%100 in 3..10, many=n%100 in 11..99, other=otherwise.</summary>
    private static KyrolusPluralCategory ResolveArabic(long n)
    {
        if (n == 0) return KyrolusPluralCategory.Zero;
        if (n == 1) return KyrolusPluralCategory.One;
        if (n == 2) return KyrolusPluralCategory.Two;

        var mod100 = n % 100;
        if (mod100 is >= 3 and <= 10) return KyrolusPluralCategory.Few;
        if (mod100 is >= 11 and <= 99) return KyrolusPluralCategory.Many;
        return KyrolusPluralCategory.Other;
    }

    /// <summary>CLDR Slavic-family rule (Russian, Ukrainian, Belarusian, Serbian, Croatian, Bosnian) for integer counts.</summary>
    private static KyrolusPluralCategory ResolveSlavic(long n)
    {
        var mod10 = n % 10;
        var mod100 = n % 100;

        if (mod10 == 1 && mod100 != 11) return KyrolusPluralCategory.One;
        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return KyrolusPluralCategory.Few;
        if (mod10 == 0 || mod10 is >= 5 and <= 9 || mod100 is >= 11 and <= 14) return KyrolusPluralCategory.Many;
        return KyrolusPluralCategory.Other;
    }
}
