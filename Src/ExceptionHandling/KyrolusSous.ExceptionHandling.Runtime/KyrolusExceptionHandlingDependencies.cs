namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusExceptionHandlingDependencies(
    KyrolusExceptionTranslator translator,
    IKyrolusErrorResponseWriter responseWriter,
    KyrolusHttpErrorContextFactory contextFactory,
    IOptions<KyrolusExceptionHandlingOptions> options,
    ILogger<KyrolusExceptionHandlingDependencies> logger)
{
    public KyrolusExceptionTranslator Translator { get; } = translator;
    public IKyrolusErrorResponseWriter ResponseWriter { get; } = responseWriter;
    public KyrolusHttpErrorContextFactory ContextFactory { get; } = contextFactory;
    public KyrolusExceptionHandlingOptions Options { get; } = options.Value;
    public ILogger<KyrolusExceptionHandlingDependencies> Logger { get; } = logger;

    /// <summary>
    /// Translates the exception and applies the shared logging policy. The single entry point used by the
    /// middleware, the MVC exception filter, and every native <see cref="IExceptionHandler"/>, so all three
    /// surfaces classify, sanitize, localize, and log exceptions identically instead of duplicating the logic.
    /// </summary>
    public KyrolusExceptionMapping TranslateAndLog(Exception exception, KyrolusErrorContext errorContext)
    {
        var mapping = Translator.TranslateToMapping(exception, errorContext);

        var isIgnoredLogType = Options.IgnoredExceptionLogTypes.Count > 0 &&
            Options.IgnoredExceptionLogTypes.Any(t => t.IsInstanceOfType(exception));

        if (mapping.ShouldLog && Options.LogUnhandledExceptions && !isIgnoredLogType)
            LogException(mapping, exception, errorContext);

        return mapping;
    }

    private void LogException(KyrolusExceptionMapping mapping, Exception exception, KyrolusErrorContext context)
    {
        var logLevel = Options.LogLevelSelector(mapping, exception);

        if (!Logger.IsEnabled(logLevel)) return;

        Logger.Log(
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
