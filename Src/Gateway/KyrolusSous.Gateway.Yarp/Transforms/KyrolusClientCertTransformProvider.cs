namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against Client Certificate Spoofing (CWE-295) in zero-trust architectures
/// by stripping untrusted client-supplied certificate headers and securely forwarding authentic mTLS connection details.
/// </summary>
public sealed class KyrolusClientCertTransformProvider : ITransformProvider
{
    private static readonly string[] UntrustedCertHeaders =
    [
        "X-Client-Cert",
        "X-Client-Cert-Thumbprint",
        "X-Client-Cert-Subject",
        "X-Client-Cert-Issuer",
        "X-SSL-Client-Verify",
        "X-SSL-Client-S-DN"
    ];

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        if (context.Route.Metadata?.TryGetValue("Kyrolus:ClientCert:Forward", out var val) == true &&
            !string.IsNullOrWhiteSpace(val) &&
            !bool.TryParse(val, out _))
        {
            context.Errors.Add(new ArgumentException($"Route '{context.Route.RouteId}' has invalid metadata 'Kyrolus:ClientCert:Forward' value '{val}'. Expected 'true' or 'false'."));
        }
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the client certificate security transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        var forwardClientCert = metadata != null &&
                                metadata.TryGetValue("Kyrolus:ClientCert:Forward", out var val) &&
                                bool.TryParse(val, out var isForward) && isForward;

        context.AddRequestTransform(transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return ValueTask.CompletedTask;
            }

            StripUntrustedHeaders(transformContext.ProxyRequest);

            if (forwardClientCert)
            {
                InjectClientCertificateHeaders(transformContext.ProxyRequest, transformContext.HttpContext.Connection.ClientCertificate);
            }

            return ValueTask.CompletedTask;
        });
    }

    private static void StripUntrustedHeaders(HttpRequestMessage proxyRequest)
    {
        for (var i = 0; i < UntrustedCertHeaders.Length; i++)
        {
            proxyRequest.Headers.Remove(UntrustedCertHeaders[i]);
        }
    }

    private static void InjectClientCertificateHeaders(HttpRequestMessage proxyRequest, System.Security.Cryptography.X509Certificates.X509Certificate2? clientCert)
    {
        if (clientCert is null)
        {
            return;
        }

        AddHeaderIfNotEmpty(proxyRequest, "X-Client-Cert-Thumbprint", clientCert.Thumbprint);
        AddHeaderIfNotEmpty(proxyRequest, "X-Client-Cert-Subject", clientCert.Subject);
        AddHeaderIfNotEmpty(proxyRequest, "X-Client-Cert-Issuer", clientCert.Issuer);
    }

    private static void AddHeaderIfNotEmpty(HttpRequestMessage request, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Headers.Add(name, value);
        }
    }
}
