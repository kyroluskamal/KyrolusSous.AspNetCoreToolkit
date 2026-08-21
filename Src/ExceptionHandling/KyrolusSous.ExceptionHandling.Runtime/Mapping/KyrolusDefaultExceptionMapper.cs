namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusDefaultExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => int.MaxValue;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        mapping = new KyrolusExceptionMapping(
            new KyrolusErrorEnvelope(
                KyrolusErrorCodes.InternalError,
                "Internal server error",
                "An unexpected error occurred.",
                context.TraceId),
            HttpStatusCode.InternalServerError);

        return true;
    }
}
