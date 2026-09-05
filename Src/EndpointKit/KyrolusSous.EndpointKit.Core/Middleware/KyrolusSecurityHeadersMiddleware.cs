using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// High-performance HTTP middleware that automatically applies hardened security response headers
/// (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, CSP) to every HTTP response.
/// </summary>
public sealed class KyrolusSecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly KyrolusSecurityHeadersOptions _options;

    public KyrolusSecurityHeadersMiddleware(RequestDelegate next, IOptions<KyrolusSecurityHeadersOptions>? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? new KyrolusSecurityHeadersOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ApplySecurityHeaders(context.Response.Headers, _options);

        await _next(context).ConfigureAwait(false);
    }

    private static void ApplySecurityHeaders(IHeaderDictionary headers, KyrolusSecurityHeadersOptions options)
    {
        if (!string.IsNullOrEmpty(options.ContentTypeOptions) && !headers.ContainsKey("X-Content-Type-Options"))
        {
            headers["X-Content-Type-Options"] = options.ContentTypeOptions;
        }

        if (!string.IsNullOrEmpty(options.FrameOptions) && !headers.ContainsKey("X-Frame-Options"))
        {
            headers["X-Frame-Options"] = options.FrameOptions;
        }

        if (!string.IsNullOrEmpty(options.XssProtection) && !headers.ContainsKey("X-XSS-Protection"))
        {
            headers["X-XSS-Protection"] = options.XssProtection;
        }

        if (!string.IsNullOrEmpty(options.ReferrerPolicy) && !headers.ContainsKey("Referrer-Policy"))
        {
            headers["Referrer-Policy"] = options.ReferrerPolicy;
        }

        if (!string.IsNullOrEmpty(options.ContentSecurityPolicy) && !headers.ContainsKey("Content-Security-Policy"))
        {
            headers["Content-Security-Policy"] = options.ContentSecurityPolicy;
        }

        if (!string.IsNullOrEmpty(options.PermissionsPolicy) && !headers.ContainsKey("Permissions-Policy"))
        {
            headers["Permissions-Policy"] = options.PermissionsPolicy;
        }
    }
}
