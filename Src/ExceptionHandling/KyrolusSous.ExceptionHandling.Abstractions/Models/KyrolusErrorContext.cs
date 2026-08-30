namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Captures the ambient HTTP request context information during exception mapping and enrichment.
/// </summary>
/// <param name="TraceId">The distributed tracing W3C activity trace ID (e.g. Activity.Current.Id).</param>
/// <param name="CorrelationId">The end-to-end correlation ID extracted from request headers.</param>
/// <param name="UserId">The authenticated user's ID claim if available.</param>
/// <param name="TenantId">The multi-tenant tenant ID if available.</param>
/// <param name="Path">The HTTP request URI path (e.g. "/api/orders/123").</param>
/// <param name="Method">The HTTP method (e.g. "POST", "GET").</param>
/// <param name="Culture">The current request culture for localized error messages.</param>
public sealed record KyrolusErrorContext(
    string? TraceId,
    string? CorrelationId,
    string? UserId,
    string? TenantId,
    string? Path,
    string? Method,
    CultureInfo? Culture);
