namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    KyrolusExceptionHandlingDependencies dependencies)
{
    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly KyrolusExceptionTranslator translator = dependencies.Translator;
    private readonly IKyrolusErrorResponseWriter responseWriter = dependencies.ResponseWriter;
    private readonly KyrolusHttpErrorContextFactory contextFactory = dependencies.ContextFactory;
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
            var mapping = translator.TranslateToMapping(ex, errorContext);

            var isIgnoredLogType = options.IgnoredExceptionLogTypes.Count > 0 &&
                                   options.IgnoredExceptionLogTypes.Any(t => t.IsInstanceOfType(ex));

            if (mapping.ShouldLog && options.LogUnhandledExceptions && !isIgnoredLogType)
                LogException(mapping, ex, errorContext);

            if (context.Response.HasStarted)
            {
                logger.LogWarning("The response has already started, the exception handling middleware cannot write the error response.");
                throw;
            }

            await responseWriter.WriteAsync(context, mapping, errorContext, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private void LogException(KyrolusExceptionMapping mapping, Exception exception, KyrolusErrorContext context)
    {
        var logLevel = options.LogLevelSelector(mapping, exception);

        if (!logger.IsEnabled(logLevel)) return;

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
