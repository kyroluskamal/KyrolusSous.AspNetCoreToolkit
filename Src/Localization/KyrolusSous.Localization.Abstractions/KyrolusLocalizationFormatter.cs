using System.Text;

namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Shared template placeholder interpolation logic (e.g. "{PropertyName}") used by every
/// <see cref="IKyrolusLocalizer"/> implementation, so the substitution rules stay consistent
/// across dictionary-based, JSON-based, and third-party-backed localizers.
/// </summary>
/// <remarks>
/// Substitution is a single pass over <c>template</c>: each <c>{token}</c> is resolved and appended to the
/// output exactly once, and the output itself is never re-scanned for further placeholders. This matters
/// because argument values can come from untrusted input (e.g. a validation failure's <c>AttemptedValue</c>) -
/// a naive "replace each key in sequence, reassigning the whole string each time" approach would let a value
/// that happens to contain literal <c>{OtherKey}</c>-shaped text get expanded again on a later iteration (a
/// "second-order" template injection), letting one argument's value corrupt or spoof another placeholder's
/// rendered content.
/// </remarks>
public static class KyrolusLocalizationFormatter
{
    /// <returns>
    /// The formatted string, with each placeholder value substituted verbatim (<c>ToString()</c>) and
    /// <b>no HTML/output encoding applied</b> - see the matching caveat on <see cref="IKyrolusLocalizer.Format"/>.
    /// </returns>
    public static string Format(string template, object? arguments)
    {
        if (string.IsNullOrEmpty(template) || arguments is null || !template.Contains('{'))
            return template;

        return arguments switch
        {
            IEnumerable<KeyValuePair<string, object?>> namedArgs
                => ReplaceNamed(template, namedArgs),

            IEnumerable<KeyValuePair<string, string>> namedArgs
                => ReplaceNamed(template, namedArgs),

            object?[] positionalArgs
                => ReplacePositional(template, positionalArgs),

            _ => throw new NotSupportedException(
                $"Cannot format placeholders from an argument of type '{arguments.GetType()}'. " +
                "Pass an IDictionary<string, object?>, IDictionary<string, string>, " +
                "another IEnumerable<KeyValuePair<string, object?>>, or an object?[] " +
                "for positional {{0}}/{{1}}/... placeholders. Arbitrary POCOs/anonymous " +
                "objects are not read via reflection because GetType().GetProperties() " +
                "is not trim/Native-AOT safe.")
        };
    }

    private static string ReplaceNamed<T>(string template, IEnumerable<KeyValuePair<string, T>> arguments)
    {
        var lookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in arguments)
            lookup[key] = value;

        return ReplaceTokens(template, token =>
            lookup.TryGetValue(token, out var value) ? (true, value?.ToString() ?? string.Empty) : (false, null));
    }

    private static string ReplacePositional(string template, object?[] arguments)
    {
        return ReplaceTokens(template, token =>
            int.TryParse(token, out var index) && index >= 0 && index < arguments.Length
                ? (true, arguments[index]?.ToString() ?? string.Empty)
                : (false, null));
    }

    /// <summary>
    /// Scans <paramref name="template"/> once, replacing each <c>{token}</c> with what <paramref name="resolve"/>
    /// returns for it (leaving the placeholder untouched when it reports "not found"). The output is built up
    /// in one <see cref="StringBuilder"/> pass and is never itself re-scanned, so a resolved value's text can't
    /// be re-interpreted as a further placeholder.
    /// </summary>
    private static string ReplaceTokens(string template, Func<string, (bool Found, string? Value)> resolve)
    {
        var result = new StringBuilder(template.Length);
        var i = 0;

        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0)
            {
                result.Append(template, i, template.Length - i);
                break;
            }

            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                result.Append(template, i, template.Length - i);
                break;
            }

            var token = template.Substring(open + 1, close - open - 1);
            var (found, value) = resolve(token);

            if (found)
            {
                result.Append(template, i, open - i);
                result.Append(value);
                i = close + 1;
            }
            else
            {
                // Not a real placeholder (e.g. a stray literal "{" earlier in the text). Emit up to and
                // including this "{" as plain text and resume scanning right after it, rather than jumping to
                // "close" - so a later, genuinely well-formed "{RealKey}" further along the template still gets
                // matched on its own instead of being swallowed as part of this non-matching span.
                result.Append(template, i, open - i + 1);
                i = open + 1;
            }
        }

        return result.ToString();
    }
}