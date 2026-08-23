namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusExceptionFilter(
    KyrolusExceptionTranslator translator,
    IKyrolusErrorResponseWriter responseWriter,
    KyrolusHttpErrorContextFactory contextFactory,
    IOptions<KyrolusExceptionHandlingOptions> options,
    ILogger<KyrolusExceptionFilter> logger) : IAsyncExceptionFilter
{
    private readonly KyrolusExceptionTranslator translator = translator;
    private readonly IKyrolusErrorResponseWriter responseWriter = responseWriter;
    private readonly KyrolusHttpErrorContextFactory contextFactory = contextFactory;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;
    private readonly ILogger<KyrolusExceptionFilter> logger = logger;

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        var errorContext = contextFactory.Create(context.HttpContext);
        var mapping = translator.TranslateToMapping(context.Exception, errorContext);

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

        context.ExceptionHandled = true;
        context.HttpContext.Response.Clear();
        await responseWriter.WriteAsync(context.HttpContext, mapping, errorContext, context.HttpContext.RequestAborted).ConfigureAwait(false);
        context.Result = new EmptyResult();
    }
}
