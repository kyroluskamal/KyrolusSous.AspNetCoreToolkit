using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Resolves tenant ID from the first subdomain segment (e.g. <c>acme.api.example.com</c> -> <c>acme</c>),
/// safely filtering out reserved infrastructure subdomains (e.g. <c>www</c>, <c>api</c>, <c>admin</c>).
/// </summary>
public sealed class KyrolusSubdomainTenantResolver : IKyrolusTenantResolver
{
    private static readonly HashSet<string> DefaultReserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "staging", "dev", "test", "gateway", "proxy"
    };

    private readonly HashSet<string> _reserved;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusSubdomainTenantResolver"/> class.
    /// </summary>
    /// <param name="reservedSubdomains">Optional custom collection of reserved subdomain prefixes to ignore.</param>
    public KyrolusSubdomainTenantResolver(IEnumerable<string>? reservedSubdomains = null)
    {
        _reserved = reservedSubdomains != null
            ? new HashSet<string>(reservedSubdomains, StringComparer.OrdinalIgnoreCase)
            : DefaultReserved;
    }

    /// <inheritdoc />
    public ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
    {
        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            System.Net.IPAddress.TryParse(host, out _))
        {
            return ValueTask.FromResult<string?>(null);
        }

        var parts = host.Split('.');
        if (parts.Length >= 3 && !_reserved.Contains(parts[0]))
        {
            var subdomain = parts[0].Trim();
            if (subdomain.Length <= 64 &&
                subdomain.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_' or '.'))
            {
                return ValueTask.FromResult<string?>(subdomain);
            }
        }

        return ValueTask.FromResult<string?>(null);
    }
}
