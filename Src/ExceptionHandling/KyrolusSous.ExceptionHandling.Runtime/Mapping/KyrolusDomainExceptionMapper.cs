namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusDomainExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -100;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is not KyrolusException kyEx)
        {
            mapping = null!;
            return false;
        }

        mapping = KyrolusExceptionMapping.Create(
            code: kyEx.Code,
            title: kyEx.Title,
            statusCode: kyEx.StatusCode,
            errors: kyEx.Errors,
            detail: kyEx.Detail,
            traceId: context.TraceId,
            metadata: KyrolusMetadataExtractor.Extract(kyEx, kyEx.Metadata))
            .AsTransient(kyEx.IsTransient)
            .WithLogging(kyEx.ShouldLog);

        return true;
    }
}
