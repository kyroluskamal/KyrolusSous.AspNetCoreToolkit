using System.Text;
using System.Text.Json;

namespace KyrolusSous.EndpointKit.Core.Pagination;

/// <summary>
/// Keyset and Cursor-based pagination utilities for high-throughput, O(1) table traversal.
/// </summary>
public static class KyrolusCursor
{
    public static string Encode<TKey>(TKey key, object? secondarySortValue = null)
    {
        var payload = new CursorPayload
        {
            K = key?.ToString(),
            S = secondarySortValue?.ToString()
        };

        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode<TKey>(string? cursor, out TKey? key, out string? secondarySortValue)
    {
        key = default;
        secondarySortValue = null;

        if (string.IsNullOrWhiteSpace(cursor)) return false;

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var json = Encoding.UTF8.GetString(bytes);
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload?.K is null) return false;

            if (typeof(TKey) == typeof(Guid))
            {
                if (Guid.TryParse(payload.K, out var g) && g is TKey gk)
                {
                    key = gk;
                }
                else return false;
            }
            else
            {
                var converted = Convert.ChangeType(payload.K, typeof(TKey));
                if (converted is TKey typedKey)
                {
                    key = typedKey;
                }
                else return false;
            }
            secondarySortValue = payload.S;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class CursorPayload
    {
        public string? K { get; set; }
        public string? S { get; set; }
    }
}

public sealed record KyrolusCursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore,
    int Limit);
