using KyrolusSous.ExceptionHandling.Mapping;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KyrolusSous.ExceptionHandling;

public sealed class KyrolusExceptionTranslator(
    KyrolusExceptionMappingService mappingService,
    IKyrolusErrorMetadataSanitizer metadataSanitizer,
    IHostEnvironment environment,
    IOptions<KyrolusExceptionHandlingOptions> options)
{
    private readonly KyrolusExceptionMappingService mappingService = mappingService;
    private readonly IKyrolusErrorMetadataSanitizer metadataSanitizer = metadataSanitizer;
    private readonly IHostEnvironment environment = environment;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;

    public KyrolusErrorResult Translate(Exception exception, KyrolusErrorContext? context = null, bool? includeDetails = null)
    {
        var resolvedContext = context ?? CreateDefaultContext();
        var mapping = mappingService.Map(exception, resolvedContext);

        var includeDetailsResolved = includeDetails ?? ShouldIncludeExceptionDetails();
        KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, resolvedContext, exception, includeDetailsResolved);
        mapping = ApplyExceptionDetails(mapping, exception, resolvedContext, includeDetailsResolved);

        return new KyrolusErrorResult(mapping.Error, mapping.StatusCode, mapping.IsTransient, exception.GetType().FullName);
    }

    private KyrolusErrorContext CreateDefaultContext()
    {
        var traceId = options.IncludeTraceId ? Activity.Current?.Id : null;
        return new KyrolusErrorContext(
            traceId,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private KyrolusExceptionMapping ApplyExceptionDetails(KyrolusExceptionMapping mapping, Exception exception, KyrolusErrorContext context, bool includeDetails)
    {
        Dictionary<string, object?>? metadata = mapping.Error.Metadata is null
            ? null
            : new Dictionary<string, object?>(mapping.Error.Metadata, StringComparer.OrdinalIgnoreCase);

        if (options.IncludeContextMetadata)
        {
            metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(context.CorrelationId))
            {
                metadata["correlationId"] = context.CorrelationId;
            }

            if (!string.IsNullOrWhiteSpace(context.UserId))
            {
                metadata["userId"] = context.UserId;
            }

            if (!string.IsNullOrWhiteSpace(context.TenantId))
            {
                metadata["tenantId"] = context.TenantId;
            }
        }

        if (includeDetails)
        {
            metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            metadata["exceptionType"] = exception.GetType().FullName;
            metadata["stackTrace"] = exception.StackTrace;

            if (exception.InnerException is not null)
            {
                metadata["innerException"] = exception.InnerException.Message;
            }
        }

        if (metadata is null)
        {
            return mapping;
        }

        var sanitized = metadataSanitizer.Sanitize(metadata, context);
        var envelope = mapping.Error with { Metadata = sanitized };
        return mapping with { Error = envelope };
    }

    private bool ShouldIncludeExceptionDetails()
    {
        if (options.IncludeExceptionDetailsInResponse)
        {
            return true;
        }

        return environment.IsDevelopment() && options.IncludeExceptionDetailsInDevelopment;
    }
}
