namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusDefaultExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => int.MaxValue;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        var errors = (exception as KyrolusException)?.Errors
                     ?? (exception as IKyrolusExceptionWithErrors)?.GetErrors();

        mapping = KyrolusExceptionMapping.Create(
            code: KyrolusErrorCodes.InternalError,
            title: "Internal server error",
            statusCode: HttpStatusCode.InternalServerError,
            errors: errors,
            detail: "An unexpected error occurred.",
            traceId: context.TraceId,
            metadata: KyrolusMetadataExtractor.Extract(exception));

        return true;
    }
}
