namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 502 (Bad Gateway) exception thrown when a downstream external 3rd-party API or microservice fails.
/// </summary>
/// <remarks>
/// Captures the target <see cref="ServiceName"/> into structured metadata and marks <c>IsTransient = true</c> for resilience/retries.
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await stripeClient.ChargeAsync(...);
/// }
/// catch (HttpRequestException ex)
/// {
///     throw new KyrolusExternalServiceException("StripePaymentService", "Failed to communicate with Stripe API.", ex);
/// }
/// </code>
/// </example>
/// <param name="serviceName">The name of the external service (e.g. "Stripe", "Twilio", "SendGrid").</param>
/// <param name="detail">An optional explanation of the external service failure.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusExternalServiceException(string serviceName, string? detail = null, Exception? innerException = null) 
: KyrolusException(
        HttpStatusCode.BadGateway,
        KyrolusErrorCodes.ExternalService,
        $"{serviceName} failure",
        detail,
        null,
        new Dictionary<string, object?> { ["serviceName"] = serviceName },
        isTransient: true,
        shouldLog: true,
        innerException)
{
    /// <summary>
    /// Gets the name of the external service that failed.
    /// </summary>
    public string ServiceName { get; } = serviceName;
}
