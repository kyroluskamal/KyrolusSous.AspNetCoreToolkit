namespace KyrolusSous.ExceptionHandling.Runtime;

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

        var errorContext = contextFactory.Create(context.HttpContext);
        var mapping = mappingService.Map(context.Exception, errorContext);
        var includeDetails = KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, environment);
        KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, errorContext, context.Exception, includeDetails);

        var isIgnoredLogType = options.IgnoredExceptionLogTypes.Count > 0 &&
                               options.IgnoredExceptionLogTypes.Any(t => t.IsInstanceOfType(context.Exception));

        if (mapping.ShouldLog && options.LogUnhandledExceptions && !isIgnoredLogType)
        {
            var logLevel = options.LogLevelSelector(mapping, context.Exception);
            if (logger.IsEnabled(logLevel))
            {
                logger.Log(
                    logLevel,
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
        }

        var enriched = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            mapping,
            context.Exception,
            errorContext,
            options,
            metadataSanitizer,
            includeDetails);

        context.ExceptionHandled = true;
        context.HttpContext.Response.Clear();
        await responseWriter.WriteAsync(context.HttpContext, enriched, errorContext, context.HttpContext.RequestAborted).ConfigureAwait(false);
        context.Result = new EmptyResult();
    }
}
