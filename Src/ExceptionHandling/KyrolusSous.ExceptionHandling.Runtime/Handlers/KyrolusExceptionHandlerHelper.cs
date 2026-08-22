namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

internal static class KyrolusExceptionHandlerHelper
{
    public static async ValueTask WriteEnvelopeAsync(
        ILogger logger,
        HttpContext httpContext,
        HttpStatusCode statusCode,
        KyrolusErrorEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(
            "Exception handled: {Code} ({StatusCode}). Path={Path}, Message={Message}",
            envelope.Code, (int)statusCode, httpContext.Request.Path, envelope.Detail);

        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = (int)statusCode;

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            envelope,
            KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope,
            cancellationToken).ConfigureAwait(false);
    }

    public static ValueTask WriteEnvelopeAsync(
        ILogger logger,
        HttpContext httpContext,
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
        => WriteEnvelopeAsync(
            logger,
            httpContext,
            statusCode,
            new KyrolusErrorEnvelope(code, title, detail, httpContext.TraceIdentifier, null, metadata),
            cancellationToken);
}
