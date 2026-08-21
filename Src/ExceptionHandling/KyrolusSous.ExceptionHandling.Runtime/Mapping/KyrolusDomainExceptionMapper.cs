namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusDomainExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -100;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is KyrolusException kyrolusException)
        {
            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(
                    kyrolusException.Code,
                    kyrolusException.Title,
                    kyrolusException.Detail ?? kyrolusException.Message,
                    context.TraceId,
                    kyrolusException.Errors),
                kyrolusException.StatusCode,
                kyrolusException.IsTransient);
            return true;
        }

        mapping = default!;
        return false;
    }
}
