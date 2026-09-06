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

        string? customCsp = null;
        string? customFrameOptions = null;
        string? customReferrerPolicy = null;
        string? customHsts = null;

        if (metadata != null)
        {
            if (metadata.TryGetValue("Kyrolus:SecurityHeaders:CSP", out var csp) && !string.IsNullOrWhiteSpace(csp))
            {
                customCsp = csp;
            }

            if (metadata.TryGetValue("Kyrolus:SecurityHeaders:FrameOptions", out var fo) && !string.IsNullOrWhiteSpace(fo))
            {
                customFrameOptions = fo;
            }

            if (metadata.TryGetValue("Kyrolus:SecurityHeaders:ReferrerPolicy", out var rp) && !string.IsNullOrWhiteSpace(rp))
            {
                customReferrerPolicy = rp;
            }

            if (metadata.TryGetValue("Kyrolus:SecurityHeaders:HSTS", out var hsts) && !string.IsNullOrWhiteSpace(hsts))
            {
                customHsts = hsts;
            }
        }

        context.AddResponseTransform(transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            var headers = transformContext.HttpContext.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = customFrameOptions ?? "DENY";
            headers["Referrer-Policy"] = customReferrerPolicy ?? "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            headers["X-XSS-Protection"] = "0";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            if (customCsp != null)
            {
                headers["Content-Security-Policy"] = customCsp;
            }

            if (transformContext.HttpContext.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = customHsts ?? "max-age=31536000; includeSubDomains";
            }

            // Information Disclosure Defense (CWE-200): Strip sensitive backend server identifiers
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");
            headers.Remove("X-AspNetMvc-Version");
            headers.Remove("X-Runtime");
            headers.Remove("X-SourceFiles");
            headers.Remove("X-Generated-By");
            headers.Remove("X-Backend-Server");
            headers.Remove("X-Backend-Host");

            return ValueTask.CompletedTask;
        });
    }
}
