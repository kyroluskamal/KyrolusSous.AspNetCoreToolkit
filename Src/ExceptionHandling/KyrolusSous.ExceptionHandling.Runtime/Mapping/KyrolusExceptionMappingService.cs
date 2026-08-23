namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusExceptionMappingService(
    IEnumerable<IKyrolusExceptionMapper> mappers,
    IKyrolusErrorLocalizer? localizer = null)
{
    private readonly IKyrolusExceptionMapper[] mappers = [.. mappers.OrderBy(m => m.Order)];
    private readonly IKyrolusErrorLocalizer? localizer = localizer;

    public KyrolusExceptionMapping Map(Exception exception, KyrolusErrorContext context)
    {
        KyrolusExceptionMapping? mapping = null;

        foreach (var mapper in mappers)
        {
            if (mapper.TryMap(exception, context, out var mapped))
            {
                mapping = mapped;
                break;
            }
        }

        var errors = (exception as KyrolusException)?.Errors
                    ?? (exception as IKyrolusExceptionWithErrors)?.GetErrors();

        mapping ??= KyrolusExceptionMapping.Create(
            code: KyrolusErrorCodes.InternalError,
            title: "Internal server error",
            statusCode: HttpStatusCode.InternalServerError,
            detail: "An unexpected error occurred.",
            traceId: context.TraceId,
            errors: errors,
            metadata: KyrolusMetadataExtractor.Extract(exception));

        return Localize(mapping, context.Culture);
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
