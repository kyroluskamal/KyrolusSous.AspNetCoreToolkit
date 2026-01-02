namespace KyrolusSous.ExceptionHandling;

public sealed class KyrolusExceptionFilter(
    KyrolusExceptionMappingService mappingService,
    IKyrolusErrorResponseWriter responseWriter,
    KyrolusHttpErrorContextFactory contextFactory,
    IKyrolusErrorMetadataSanitizer metadataSanitizer,
    IHostEnvironment environment,
    IOptions<KyrolusExceptionHandlingOptions> options,
    ILogger<KyrolusExceptionFilter> logger) : IAsyncExceptionFilter
{
    private readonly KyrolusExceptionMappingService mappingService = mappingService;
    private readonly IKyrolusErrorResponseWriter responseWriter = responseWriter;
    private readonly KyrolusHttpErrorContextFactory contextFactory = contextFactory;
    private readonly IKyrolusErrorMetadataSanitizer metadataSanitizer = metadataSanitizer;
    private readonly IHostEnvironment environment = environment;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;
    private readonly ILogger<KyrolusExceptionFilter> logger = logger;

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        var errorContext = contextFactory.Create(context.Exception);
        var mapping = mappingService.Map(context.Exception, errorContext);
        var includeDetails = ShouldIncludeExceptionDetails();
        KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, errorContext, context.Exception, includeDetails);

        if (mapping.ShouldLog && options.LogUnhandledExceptions && logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                context.Exception,
                "Unhandled exception mapped to {ErrorCode} ({StatusCode}). TraceId={TraceId}, CorrelationId={CorrelationId}, UserId={UserId}, TenantId={TenantId}, Path={Path}, Method={Method}",
                mapping.Error.Code,
                (int)mapping.StatusCode,
                errorContext.TraceId,
                errorContext.CorrelationId,
                errorContext.UserId,
                errorContext.TenantId,
                errorContext.Path,
                errorContext.Method);
        }

        var enriched = ApplyExceptionDetails(mapping, context.Exception, errorContext, includeDetails);

        context.ExceptionHandled = true;
        context.HttpContext.Response.Clear();
        await responseWriter.WriteAsync(context.HttpContext, enriched, errorContext, context.HttpContext.RequestAborted).ConfigureAwait(false);
        context.Result = new EmptyResult();
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
