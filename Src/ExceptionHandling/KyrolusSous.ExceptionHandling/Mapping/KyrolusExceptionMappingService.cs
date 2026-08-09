using System.Globalization;

namespace KyrolusSous.ExceptionHandling.Mapping;

public sealed class KyrolusExceptionMappingService(
    IEnumerable<IKyrolusExceptionMapper> mappers,
    IKyrolusErrorLocalizer? localizer = null)
{
    private readonly IKyrolusExceptionMapper[] mappers = [.. mappers.OrderBy(m => m.Order)];
    private readonly IKyrolusErrorLocalizer? localizer = localizer;

    public KyrolusExceptionMapping Map(Exception exception, KyrolusErrorContext context)
    {
        foreach (var mapper in mappers)
        {
            if (mapper.TryMap(exception, context, out var mapping))
            {
                return Localize(mapping, context.Culture);
            }
        }

        var fallback = new KyrolusExceptionMapping(
            new KyrolusErrorEnvelope(
                KyrolusErrorCodes.InternalError,
                "Internal server error",
                "An unexpected error occurred.",
                context.TraceId),
            HttpStatusCode.InternalServerError);

        return Localize(fallback, context.Culture);
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
