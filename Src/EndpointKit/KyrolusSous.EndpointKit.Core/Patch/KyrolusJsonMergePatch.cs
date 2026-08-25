using System.Text.Json;

namespace KyrolusSous.EndpointKit.Core.Patch;

/// <summary>
/// RFC 7396 JSON Merge Patch document processor.
/// Allows distinguishing explicitly nullified properties from unchanged/omitted properties.
/// </summary>
public static class KyrolusJsonMergePatch
{
    public static Dictionary<string, object?> ParseMergePatch(JsonElement jsonElement)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var prop in jsonElement.EnumerateObject())
        {
            result[prop.Name] = ConvertJsonValue(prop.Value);
        }

        return result;
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => ParseMergePatch(element),
            _ => element.GetRawText()
        };
    }
}
