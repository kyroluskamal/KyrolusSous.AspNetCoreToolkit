namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// MVC and API Controller action filter that handles unhandled action exceptions before they leave the MVC pipeline.
/// </summary>
/// <remarks>
/// Use when running in standard MVC / Web API controllers where controller-level filter execution is preferred.
/// </remarks>
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

    /// <summary>
    /// Executes when an unhandled exception occurs inside a controller action.
    /// </summary>
    /// <param name="context">The MVC exception context.</param>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.ExceptionHandled) return;

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
