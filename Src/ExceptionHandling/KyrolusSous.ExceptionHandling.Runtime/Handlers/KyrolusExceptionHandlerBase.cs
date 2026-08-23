namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

public abstract class KyrolusExceptionHandlerBase<TException>(
    ILogger logger,
    HttpStatusCode statusCode,
    string errorCode,
    string title) : IExceptionHandler
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

        var metadata = KyrolusMetadataExtractor.Extract(typedException);
        var errors = ExtractErrors(typedException);
        var envelope = new KyrolusErrorEnvelope(
            errorCode,
            title,
            typedException.Message,
            httpContext.TraceIdentifier,
            errors,
            metadata);

        await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
            logger,
            httpContext,
            statusCode,
            envelope,
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
