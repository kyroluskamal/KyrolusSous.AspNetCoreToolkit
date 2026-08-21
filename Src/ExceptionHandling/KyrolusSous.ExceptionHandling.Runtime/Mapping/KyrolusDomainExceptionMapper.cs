namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusDomainExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -100;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is KyrolusException kyrolusException)
        {
            var title = kyrolusException.Title;
            var statusCode = kyrolusException.StatusCode;

            if (KyrolusErrorCodeRegistry.TryGet(kyrolusException.Code, out var definition))
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = definition.Title;
                }

                if (statusCode == 0)
                {
                    statusCode = definition.StatusCode;
                }
            }

            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(
                    kyrolusException.Code,
                    title,
                    kyrolusException.Detail ?? kyrolusException.Message,
                    context.TraceId,
                    kyrolusException.Errors),
                statusCode,
                kyrolusException.IsTransient);
            return true;
        }

        mapping = default!;
        return false;
    }
}
