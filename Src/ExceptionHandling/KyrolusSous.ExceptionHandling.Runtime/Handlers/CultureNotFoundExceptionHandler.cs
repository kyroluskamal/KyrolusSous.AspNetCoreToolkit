namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public class CultureNotFoundExceptionHandler(ILogger<CultureNotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is CultureNotFoundException cultureEx)
        {
            var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(cultureEx.InvalidCultureName))
            {
                metadata["invalidCultureName"] = cultureEx.InvalidCultureName;
            }
            if (!string.IsNullOrWhiteSpace(cultureEx.ParamName))
            {
                metadata["paramName"] = cultureEx.ParamName;
            }

            var envelope = new KyrolusErrorEnvelope(
                KyrolusErrorCodes.BadRequest,
                "Invalid culture",
                cultureEx.Message,
                httpContext.TraceIdentifier,
                null,
                metadata.Count > 0 ? metadata : null);

            await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
                logger,
                httpContext,
                HttpStatusCode.BadRequest,
                envelope,
                cancellationToken).ConfigureAwait(false);

            return true;
        }

        return false;
    }
}
