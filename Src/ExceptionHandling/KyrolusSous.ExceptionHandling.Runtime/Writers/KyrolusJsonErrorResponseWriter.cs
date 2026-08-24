namespace KyrolusSous.ExceptionHandling.Runtime.Writers;

public sealed class KyrolusJsonErrorResponseWriter : IKyrolusErrorResponseWriter
{
    public Task WriteAsync(HttpContext context, KyrolusExceptionMapping mapping, KyrolusErrorContext errorContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)mapping.StatusCode;

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            mapping.Error,
            KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope,
            cancellationToken);
    }
}
