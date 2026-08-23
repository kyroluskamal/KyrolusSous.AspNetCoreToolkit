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
}
