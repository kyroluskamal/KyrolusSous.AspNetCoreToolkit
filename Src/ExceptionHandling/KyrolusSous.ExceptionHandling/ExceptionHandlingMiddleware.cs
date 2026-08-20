namespace KyrolusSous.ExceptionHandling;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    KyrolusExceptionHandlingDependencies dependencies)
{
    private readonly RequestDelegate next = next;
    private readonly KyrolusExceptionMappingService mappingService = dependencies.MappingService;
    private readonly IKyrolusErrorResponseWriter responseWriter = dependencies.ResponseWriter;
    private readonly KyrolusHttpErrorContextFactory contextFactory = dependencies.ContextFactory;
    private readonly IKyrolusErrorMetadataSanitizer metadataSanitizer = dependencies.MetadataSanitizer;
    private readonly IHostEnvironment environment = dependencies.Environment;
    private readonly KyrolusExceptionHandlingOptions options = dependencies.Options;
    private readonly ILogger<ExceptionHandlingMiddleware> logger = dependencies.Logger;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var errorContext = contextFactory.Create(ex);
            var mapping = mappingService.Map(ex, errorContext);
            var includeDetails = ShouldIncludeExceptionDetails();
            KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, errorContext, ex, includeDetails);
            mapping = ApplyExceptionDetails(mapping, ex, errorContext, includeDetails);

            if (mapping.ShouldLog && options.LogUnhandledExceptions)
            {
                LogException(mapping, ex, errorContext);
            }

            await responseWriter.WriteAsync(context, mapping, errorContext, context.RequestAborted).ConfigureAwait(false);
        }
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

    private void LogException(KyrolusExceptionMapping mapping, Exception exception, KyrolusErrorContext context)
    {
        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        logger.LogError(
            exception,
            "Unhandled exception mapped to {ErrorCode} ({StatusCode}). TraceId={TraceId}, CorrelationId={CorrelationId}, UserId={UserId}, TenantId={TenantId}, Path={Path}, Method={Method}",
            mapping.Error.Code,
            (int)mapping.StatusCode,
            context.TraceId,
            context.CorrelationId,
            context.UserId,
            context.TenantId,
            context.Path,
            context.Method);
    }
}
