namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusExceptionHandlingDependencies(
    KyrolusExceptionMappingService mappingService,
    IKyrolusErrorResponseWriter responseWriter,
    KyrolusHttpErrorContextFactory contextFactory,
    IKyrolusErrorMetadataSanitizer metadataSanitizer,
    IHostEnvironment environment,
    IOptions<KyrolusExceptionHandlingOptions> options,
    ILogger<KyrolusExceptionHandlingDependencies> logger)
{
    public KyrolusExceptionMappingService MappingService { get; } = mappingService;
    public IKyrolusErrorResponseWriter ResponseWriter { get; } = responseWriter;
    public KyrolusHttpErrorContextFactory ContextFactory { get; } = contextFactory;
    public IKyrolusErrorMetadataSanitizer MetadataSanitizer { get; } = metadataSanitizer;
    public IHostEnvironment Environment { get; } = environment;
    public KyrolusExceptionHandlingOptions Options { get; } = options.Value;
    public ILogger<KyrolusExceptionHandlingDependencies> Logger { get; } = logger;
}
