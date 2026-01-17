using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KyrolusSous.CQRS.Marten.Query;

internal static class KyrolusSeekToken
{
    public static string Encode(IReadOnlyDictionary<string, object?> values, bool descending)
    {
        var payload = new SeekTokenPayload(descending, Serialize(values));
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return Base64UrlEncode(Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(string token, out SeekTokenPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (!TryBase64UrlDecode(token, out var bytes)) return false;
        try
        {
            payload = JsonSerializer.Deserialize<SeekTokenPayload>(bytes, JsonOptions)!;
            return payload is not null;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string?> Serialize(IReadOnlyDictionary<string, object?> values)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            dict[pair.Key] = SerializeValue(pair.Value);
        }
        return dict;
    }

    private static string? SerializeValue(object? value)
    {
        if (value is null) return null;
        return value switch
        {
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        var encoded = Convert.ToBase64String(bytes);
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryBase64UrlDecode(string token, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var base64 = token.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal sealed record SeekTokenPayload(bool Descending, Dictionary<string, string?> Keys);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
