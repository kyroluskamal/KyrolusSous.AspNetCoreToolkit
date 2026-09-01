namespace KyrolusSous.ExceptionHandling.Runtime.Writers;

public sealed class KyrolusJsonErrorResponseWriter : IKyrolusErrorResponseWriter
{
    public Task WriteAsync(HttpContext httpContext, KyrolusExceptionMapping mapping, KyrolusErrorContext errorContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Response.HasStarted)
            return Task.CompletedTask;

        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = (int)mapping.StatusCode;

        if (mapping.RetryAfter is { } retryAfter)
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        return JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            mapping.Error,
            KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope,
            cancellationToken);
    }
}
