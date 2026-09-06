namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that hardens edge security by injecting baseline defensive HTTP response headers
/// into all outgoing reverse proxy responses before delivery to client browsers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Injected Security Headers:</b><br/>
/// <list type="bullet">
/// <item><description><c>X-Content-Type-Options: nosniff</c> - Prevents browsers from MIME-sniffing a response away from the declared content-type.</description></item>
/// <item><description><c>X-Frame-Options: DENY</c> - Defends against Clickjacking attacks by forbidding the page from being rendered inside an iframe or frame.</description></item>
/// <item><description><c>Referrer-Policy: strict-origin-when-cross-origin</c> - Protects sensitive URLs and tokens from leaking in the Referer header to external origins.</description></item>
/// <item><description><c>Permissions-Policy: accelerometer=(), camera=(), ...</c> - Disables unneeded browser hardware capabilities to limit exploit blast radius.</description></item>
/// <item><description><c>X-XSS-Protection: 0</c> - Modern OWASP guideline disabling buggy legacy XSS filter audits in favor of Content Security Policy.</description></item>
/// <item><description><c>Strict-Transport-Security: max-age=31536000; includeSubDomains</c> - Enforces HTTPS and defends against SSL-stripping man-in-the-middle attacks.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class KyrolusSecurityHeadersTransformProvider : ITransformProvider
{
    private static readonly string[] SensitiveServerHeaders =
    [
        "Server",
        "X-Powered-By",
        "X-AspNet-Version",
        "X-AspNetMvc-Version",
        "X-Runtime",
        "X-SourceFiles",
        "X-Generated-By",
        "X-Backend-Server",
        "X-Backend-Host"
    ];

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the security headers response transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        var customCsp = GetMetadataValue(metadata, "Kyrolus:SecurityHeaders:CSP");
        var customFrameOptions = GetMetadataValue(metadata, "Kyrolus:SecurityHeaders:FrameOptions");
        var customReferrerPolicy = GetMetadataValue(metadata, "Kyrolus:SecurityHeaders:ReferrerPolicy");
        var customHsts = GetMetadataValue(metadata, "Kyrolus:SecurityHeaders:HSTS");

        context.AddResponseTransform(transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            var headers = transformContext.HttpContext.Response.Headers;
            InjectDefaultSecurityHeaders(headers, customFrameOptions, customReferrerPolicy, customCsp);

            if (transformContext.HttpContext.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = customHsts ?? "max-age=31536000; includeSubDomains";
            }

            StripSensitiveServerHeaders(headers);
            return ValueTask.CompletedTask;
        });
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, string>? metadata, string key) =>
        metadata != null && metadata.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : null;

    private static void InjectDefaultSecurityHeaders(IHeaderDictionary headers, string? frameOptions, string? referrerPolicy, string? csp)
    {
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = frameOptions ?? "DENY";
        headers["Referrer-Policy"] = referrerPolicy ?? "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
        headers["X-XSS-Protection"] = "0";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        if (csp != null)
        {
            headers["Content-Security-Policy"] = csp;
        }
    }

    private static void StripSensitiveServerHeaders(IHeaderDictionary headers)
    {
        for (var i = 0; i < SensitiveServerHeaders.Length; i++)
        {
            headers.Remove(SensitiveServerHeaders[i]);
        }
    }
}
