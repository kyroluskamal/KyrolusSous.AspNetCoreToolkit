using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Conditional;

/// <summary>
/// HTTP conditional request evaluator supporting ETag, If-Match (optimistic concurrency),
/// and If-None-Match (HTTP 304 Not Modified caching).
/// </summary>
public static class KyrolusConditionalRequest
{
    public static string GenerateEtag(string rawValue)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        var hex = Convert.ToHexString(hashBytes)[..16];
        return $"\"{hex}\"";
    }

    public static string FormatEtag(object? rowVersion)
    {
        if (rowVersion is null) return string.Empty;
        if (rowVersion is byte[] bytes)
        {
            return $"\"{Convert.ToBase64String(bytes)}\"";
        }
        return $"\"{rowVersion}\"";
    }

    public static bool IsNotModified(HttpRequest request, string currentEtag)
    {
        if (!request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch)) return false;
        var headerVal = ifNoneMatch.ToString().Trim();
        if (headerVal == "*") return true;

        var tokens = headerVal.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(t => MatchEtag(t, currentEtag));
    }

    public static bool IsPreconditionFailed(HttpRequest request, string currentEtag)
    {
        if (!request.Headers.TryGetValue("If-Match", out var ifMatch)) return false;
        var headerVal = ifMatch.ToString().Trim();
        if (headerVal == "*") return false;

        var tokens = headerVal.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return !tokens.Any(t => MatchEtag(t, currentEtag));
    }

    private static bool MatchEtag(string requested, string current)
    {
        var cleanReq = requested.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ? requested[2..] : requested;
        var cleanCur = current.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ? current[2..] : current;
        return string.Equals(cleanReq.Trim('"'), cleanCur.Trim('"'), StringComparison.OrdinalIgnoreCase);
    }
}
