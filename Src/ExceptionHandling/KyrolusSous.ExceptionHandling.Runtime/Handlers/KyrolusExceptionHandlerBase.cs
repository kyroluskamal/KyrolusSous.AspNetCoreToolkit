namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

/// <summary>
/// Base class for ASP.NET Core native <see cref="IExceptionHandler"/> implementations with localization,
/// metadata extraction, and security sanitization.
/// </summary>
/// <typeparam name="TException">The specific exception type handled.</typeparam>
public abstract class KyrolusExceptionHandlerBase<TException>(
    ILogger logger,
    HttpStatusCode statusCode,
    string errorCode,
    string title,
    IKyrolusErrorLocalizer? localizer = null,
    IKyrolusErrorMetadataSanitizer? sanitizer = null,
    KyrolusHttpErrorContextFactory? contextFactory = null) : IExceptionHandler
    where TException : Exception
{
    protected virtual IReadOnlyList<KyrolusErrorItem>? ExtractErrors(TException exception)
    {
        if (exception is KyrolusException kyrolusException)
            return kyrolusException.Errors;

        if (exception is IKyrolusExceptionWithErrors errorsException)
            return errorsException.GetErrors();

        return null;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TException typedException) return false;

        var errorContext = contextFactory?.Create(httpContext)
            ?? new KyrolusErrorContext(httpContext.TraceIdentifier, null, null, null, httpContext.Request.Path, httpContext.Request.Method, null);

        var rawMetadata = KyrolusMetadataExtractor.Extract(typedException);
        var sanitizedMetadata = (sanitizer is not null && rawMetadata is { Count: > 0 })
            ? sanitizer.Sanitize(rawMetadata, errorContext)
            : rawMetadata;

        var errors = ExtractErrors(typedException);
        var rawEnvelope = new KyrolusErrorEnvelope(
            errorCode,
            title,
            typedException.Message,
            errorContext.TraceId ?? httpContext.TraceIdentifier,
            errors,
            sanitizedMetadata);

        var envelope = localizer.Localize(rawEnvelope, errorContext.Culture);

        await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
            logger,
            httpContext,
            statusCode,
            envelope,
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
