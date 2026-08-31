namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Shared template placeholder interpolation logic (e.g. "{PropertyName}") used by every
/// <see cref="IKyrolusLocalizer"/> implementation, so the substitution rules stay consistent
/// across dictionary-based, JSON-based, and third-party-backed localizers.
/// </summary>
public static class KyrolusLocalizationFormatter
{
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
        foreach (var (key, value) in arguments)
            template = Replace(template, key, value);
        return template;
    }

    private static string ReplacePositional(string template, object?[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
            template = Replace(template, i, arguments[i]);

        return template;
    }

    private static string Replace(string template, string key, object? value)
    => template.Replace($"{{{key}}}", value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string Replace(string template, int index, object? value)
    => template.Replace($"{{{index}}}", value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}