namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

/// <summary>
/// Service responsible for mapping caught CLR exceptions into a preliminary <see cref="KyrolusExceptionMapping"/>
/// using registered <see cref="IKyrolusExceptionMapper"/> instances.
/// </summary>
public sealed class KyrolusExceptionMappingService(
    IEnumerable<IKyrolusExceptionMapper> mappers)
{
    private readonly IKyrolusExceptionMapper[] mappers = [.. mappers.OrderBy(m => m.Order)];

    /// <summary>
    /// Evaluates registered mappers to convert an exception into a <see cref="KyrolusExceptionMapping"/>.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <returns>The mapped exception result.</returns>
    public KyrolusExceptionMapping Map(Exception exception, KyrolusErrorContext context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(context);

        var unwrapped = UnwrapException(exception);
        KyrolusExceptionMapping? mapping = null;

        foreach (var mapper in mappers)
            if (mapper.TryMap(unwrapped, context, out var mapped))
            {
                mapping = mapped;
                break;
            }

        if (mapping is null && !ReferenceEquals(unwrapped, exception))
            foreach (var mapper in mappers)
                if (mapper.TryMap(exception, context, out var mapped))
                {
                    mapping = mapped;
                    break;
                }

        var targetEx = (mapping is not null && !ReferenceEquals(unwrapped, exception)) ? unwrapped : exception;
        var errors = (targetEx as KyrolusException)?.Errors
                    ?? (targetEx as IKyrolusExceptionWithErrors)?.GetErrors();

        mapping ??= KyrolusExceptionMapping.Create(
            code: KyrolusErrorCodes.InternalError,
            title: "Internal server error",
            statusCode: HttpStatusCode.InternalServerError,
            detail: "An unexpected error occurred.",
            traceId: context.TraceId,
            errors: errors,
            metadata: KyrolusMetadataExtractor.Extract(targetEx));

        return mapping;
    }

    private static Exception UnwrapException(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
            exception = inner;

        if (exception is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();
            if (flattened.InnerExceptions.Count == 1 && flattened.InnerExceptions[0] is { } singleInner)
                return UnwrapException(singleInner);
        }

        return exception;
    }
}
