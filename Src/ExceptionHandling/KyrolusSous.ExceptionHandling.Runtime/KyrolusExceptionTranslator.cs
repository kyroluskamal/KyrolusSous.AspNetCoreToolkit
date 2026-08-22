namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusExceptionTranslator(
    KyrolusExceptionMappingService mappingService,
    IKyrolusErrorMetadataSanitizer metadataSanitizer,
    IHostEnvironment environment,
    IOptions<KyrolusExceptionHandlingOptions> options)
{
    private readonly KyrolusExceptionMappingService mappingService = mappingService;
    private readonly IKyrolusErrorMetadataSanitizer metadataSanitizer = metadataSanitizer;
    private readonly IHostEnvironment environment = environment;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;

    public KyrolusErrorResult Translate(Exception exception, KyrolusErrorContext? context = null, bool? includeDetails = null)
    {
        var resolvedContext = context ?? CreateDefaultContext();
        var mapping = mappingService.Map(exception, resolvedContext);

        var includeDetailsResolved = includeDetails ?? KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, environment);
        KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, resolvedContext, exception, includeDetailsResolved);
        mapping = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(mapping, exception, resolvedContext, options, metadataSanitizer, includeDetailsResolved);

        return new KyrolusErrorResult(mapping.Error, mapping.StatusCode, mapping.IsTransient, exception.GetType().FullName);
    }

    private KyrolusErrorContext CreateDefaultContext()
    {
        var traceId = options.IncludeTraceId ? Activity.Current?.Id : null;
        return new KyrolusErrorContext(
            traceId,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
