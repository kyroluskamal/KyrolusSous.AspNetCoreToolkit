using System.Reflection;

namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusExceptionMappingService(
    IEnumerable<IKyrolusExceptionMapper> mappers,
    IKyrolusErrorLocalizer? localizer = null)
{
    private readonly IKyrolusExceptionMapper[] mappers = [.. mappers.OrderBy(m => m.Order)];
    private readonly IKyrolusErrorLocalizer? localizer = localizer;

    public KyrolusExceptionMapping Map(Exception exception, KyrolusErrorContext context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(context);

        var unwrapped = UnwrapException(exception);
        KyrolusExceptionMapping? mapping = null;

        foreach (var mapper in mappers)
        {
            if (mapper.TryMap(unwrapped, context, out var mapped))
            {
                mapping = mapped;
                break;
            }
        }

        if (mapping is null && !ReferenceEquals(unwrapped, exception))
        {
            foreach (var mapper in mappers)
            {
                if (mapper.TryMap(exception, context, out var mapped))
                {
                    mapping = mapped;
                    break;
                }
            }
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

        return Localize(mapping, context.Culture);
    }

    private static Exception UnwrapException(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
        {
            exception = inner;
        }

        if (exception is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten();
            if (flattened.InnerExceptions.Count == 1 && flattened.InnerExceptions[0] is { } singleInner)
            {
                return UnwrapException(singleInner);
            }
        }

        return exception;
    }

    private KyrolusExceptionMapping Localize(KyrolusExceptionMapping mapping, CultureInfo? culture)
    {
        if (localizer is null)
        {
            return mapping;
        }

        var title = localizer.Localize(mapping.Error.Code, mapping.Error.Title, culture) ?? mapping.Error.Title;
        var detail = localizer.Localize($"{mapping.Error.Code}.detail", mapping.Error.Detail, culture) ?? mapping.Error.Detail;

        var envelope = mapping.Error with { Title = title, Detail = detail };
        return mapping with { Error = envelope };
    }
}
