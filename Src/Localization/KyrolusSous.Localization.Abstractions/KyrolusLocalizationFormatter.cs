using System.Reflection;

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

        if (arguments is IDictionary<string, object?> dictObj)
        {
            var result = template;
            foreach (var (k, v) in dictObj)
                result = result.Replace($"{{{k}}}", v?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return result;
        }

        if (arguments is IDictionary<string, string> dictStr)
        {
            var result = template;
            foreach (var (k, v) in dictStr)
                result = result.Replace($"{{{k}}}", v ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return result;
        }

        if (arguments is object?[] array)
        {
            for (var i = 0; i < array.Length; i++)
                template = template.Replace($"{{{i}}}", array[i]?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return template;
        }

        var properties = arguments.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var formattedResult = template;
        foreach (var prop in properties)
        {
            var val = prop.GetValue(arguments)?.ToString() ?? string.Empty;
            formattedResult = formattedResult.Replace($"{{{prop.Name}}}", val, StringComparison.OrdinalIgnoreCase);
        }

        return formattedResult;
    }
}
