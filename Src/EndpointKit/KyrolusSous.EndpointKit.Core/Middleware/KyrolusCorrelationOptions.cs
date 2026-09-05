namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Configuration options for <see cref="KyrolusCorrelationMiddleware"/>.
/// </summary>
public sealed class KyrolusCorrelationOptions
{
    /// <summary>
    /// Gets or sets the HTTP header name used to pass the Correlation ID. Defaults to "X-Correlation-ID".
    /// </summary>
    public string HeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets whether to include the Correlation ID in the outgoing HTTP response headers. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeInResponse { get; set; } = true;

    /// <summary>
    /// Gets or sets the key used to store the Correlation ID in <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>.
    /// Defaults to "Kyrolus_CorrelationId".
    /// </summary>
    public string ItemKey { get; set; } = "Kyrolus_CorrelationId";

    /// <summary>
    /// Gets or sets whether to enforce setting the W3C OpenTelemetry activity tag <c>correlation.id</c>.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool SetActivityTag { get; set; } = true;
}
