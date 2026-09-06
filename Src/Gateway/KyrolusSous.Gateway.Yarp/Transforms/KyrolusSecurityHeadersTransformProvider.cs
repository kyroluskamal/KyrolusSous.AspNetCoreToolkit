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
        context.AddResponseTransform(transformContext =>
        {
            var headers = transformContext.HttpContext.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            headers["X-XSS-Protection"] = "0";
            return ValueTask.CompletedTask;
        });
    }
}
