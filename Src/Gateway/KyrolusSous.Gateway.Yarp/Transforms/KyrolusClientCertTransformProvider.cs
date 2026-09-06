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
    public void ValidateRoute(TransformRouteValidationContext context) { }

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

            // 1. Defend against spoofing: Strip untrusted client-supplied headers
            for (var i = 0; i < UntrustedCertHeaders.Length; i++)
            {
                transformContext.ProxyRequest.Headers.Remove(UntrustedCertHeaders[i]);
            }

            // 2. If client cert forwarding is enabled and the TLS connection has a client certificate, inject authentic details
            var clientCert = transformContext.HttpContext.Connection.ClientCertificate;
            if (forwardClientCert && clientCert is not null)
            {
                if (!string.IsNullOrWhiteSpace(clientCert.Thumbprint))
                {
                    transformContext.ProxyRequest.Headers.Add("X-Client-Cert-Thumbprint", clientCert.Thumbprint);
                }

                if (!string.IsNullOrWhiteSpace(clientCert.Subject))
                {
                    transformContext.ProxyRequest.Headers.Add("X-Client-Cert-Subject", clientCert.Subject);
                }

                if (!string.IsNullOrWhiteSpace(clientCert.Issuer))
                {
                    transformContext.ProxyRequest.Headers.Add("X-Client-Cert-Issuer", clientCert.Issuer);
                }
            }

            return ValueTask.CompletedTask;
        });
    }
}
