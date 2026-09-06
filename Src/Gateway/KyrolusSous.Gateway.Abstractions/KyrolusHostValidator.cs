namespace KyrolusSous.Gateway.Abstractions;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Provides high-performance regex validation and normalization for gateway route Host matching criteria.
/// Ensures hostnames conform to RFC 1123, RFC 3986, and YARP routing rules, catching common developer
/// configuration errors such as accidental schemes (<c>http://</c>), path slashes (<c>/</c>), or query strings (<c>?</c>).
/// </summary>
public static partial class KyrolusHostValidator
{
    [GeneratedRegex(
        @"^(\*|(\*\.)?([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?|(\d{1,3}\.){3}\d{1,3}|\[[0-9a-fA-F:]+\])(:(?<port>\d{1,5}))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HostRegex();

    /// <summary>
    /// Validates and normalizes an inbound gateway route host pattern.
    /// </summary>
    /// <param name="host">The host string to validate (e.g., <c>"api.example.com"</c>, <c>"*.example.com"</c>, <c>"localhost:5000"</c>).</param>
    /// <param name="paramName">The parameter name to report in exceptions.</param>
    /// <returns>The normalized, lowercase hostname pattern.</returns>
    /// <exception cref="ArgumentException">Thrown when the host format is invalid or contains schemes, slashes, or query strings.</exception>
    public static string Validate(string? host, string paramName = "host")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host, paramName);
        var trimmed = host.Trim();

        if (trimmed.Contains("://", StringComparison.Ordinal) ||
            trimmed.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("wss:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name must not include a URI scheme (like 'http://' or 'https://'). " +
                $"Specify only the hostname and optional port (e.g. 'api.example.com' or 'api.example.com:5000').",
                paramName);
        }

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name must not contain path slashes ('/' or '\\'). " +
                $"Paths must be specified in the route Path pattern, not in Hosts.",
                paramName);
        }

        if (trimmed.Contains('?'))
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name must not contain query parameters ('?').",
                paramName);
        }

        if (trimmed.Contains('#'))
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name must not contain URL fragments ('#').",
                paramName);
        }

        if (trimmed.Contains(' '))
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name must not contain whitespace.",
                paramName);
        }

        if (trimmed.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name must not contain consecutive dots ('..').",
                paramName);
        }

        var match = HostRegex().Match(trimmed);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Host '{trimmed}' is invalid: host name does not match standard RFC 1123 hostname or wildcard format (e.g. 'api.example.com', '*.example.com', or 'localhost:5000').",
                paramName);
        }

        var portGroup = match.Groups["port"];
        if (portGroup.Success)
        {
            if (!int.TryParse(portGroup.Value, out var port) || port is < 1 or > 65535)
            {
                throw new ArgumentException(
                    $"Host '{trimmed}' has an invalid port number '{portGroup.Value}'. Port must be between 1 and 65535.",
                    paramName);
            }
        }

        var rawHostWithoutPort = portGroup.Success
            ? trimmed[..(portGroup.Index - 1)]
            : trimmed;

        // Additional sanity check for bracketed IPv6 addresses
        if (rawHostWithoutPort.StartsWith('[') && rawHostWithoutPort.EndsWith(']'))
        {
            var ipStr = rawHostWithoutPort[1..^1];
            if (!IPAddress.TryParse(ipStr, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                throw new ArgumentException(
                    $"Host '{trimmed}' contains an invalid IPv6 address format.",
                    paramName);
            }
        }

        // Additional sanity check for IPv4 addresses
        if (char.IsDigit(rawHostWithoutPort[0]) && rawHostWithoutPort.Contains('.'))
        {
            if (!IPAddress.TryParse(rawHostWithoutPort, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new ArgumentException(
                    $"Host '{trimmed}' contains an invalid IPv4 address format.",
                    paramName);
            }
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Attempts to validate and normalize a host string, returning a boolean indicating success.
    /// </summary>
    public static bool TryValidate(
        string? host,
        [NotNullWhen(true)] out string? normalizedHost,
        [NotNullWhen(false)] out string? errorMessage)
    {
        try
        {
            normalizedHost = Validate(host);
            errorMessage = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            normalizedHost = null;
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Checks whether the specified host string is a valid gateway route host pattern.
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? host) =>
        TryValidate(host, out _, out _);
}
