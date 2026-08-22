namespace KyrolusSous.ExceptionHandling.Runtime;

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
    private readonly ILogger logger = dependencies.Logger;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var errorContext = contextFactory.Create(context);
            var mapping = mappingService.Map(ex, errorContext);
            var includeDetails = KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, environment);
            KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, errorContext, ex, includeDetails);
            mapping = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(mapping, ex, errorContext, options, metadataSanitizer, includeDetails);

            var isIgnoredLogType = options.IgnoredExceptionLogTypes.Count > 0 &&
                                   options.IgnoredExceptionLogTypes.Any(t => t.IsInstanceOfType(ex));

            if (mapping.ShouldLog && options.LogUnhandledExceptions && !isIgnoredLogType)
            {
                LogException(mapping, ex, errorContext);
            }

            await responseWriter.WriteAsync(context, mapping, errorContext, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private void LogException(KyrolusExceptionMapping mapping, Exception exception, KyrolusErrorContext context)
    {
        var logLevel = options.LogLevelSelector(mapping, exception);

        if (!logger.IsEnabled(logLevel))
        {
            return;
        }

        logger.Log(
            logLevel,
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
