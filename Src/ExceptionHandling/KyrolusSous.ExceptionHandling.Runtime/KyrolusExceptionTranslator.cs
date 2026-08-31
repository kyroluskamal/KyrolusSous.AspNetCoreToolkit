namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// Core translation engine that coordinates exception mapping, metadata sanitization, activity tracing enrichment, and detail resolution.
/// </summary>
public sealed class KyrolusExceptionTranslator(
    KyrolusExceptionMappingService mappingService,
    IKyrolusErrorMetadataSanitizer metadataSanitizer,
    IHostEnvironment environment,
    IOptions<KyrolusExceptionHandlingOptions> options,
    IKyrolusLocalizer? localizer = null)
{
    private readonly KyrolusExceptionMappingService mappingService = mappingService;
    private readonly IKyrolusErrorMetadataSanitizer metadataSanitizer = metadataSanitizer;
    private readonly IHostEnvironment environment = environment;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;
    private readonly IKyrolusLocalizer? localizer = localizer;

    /// <summary>
    /// Translates an exception into an enriched <see cref="KyrolusExceptionMapping"/> including status code and logging directives.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <param name="includeDetails">Explicit override for whether to include stack traces.</param>
    /// <returns>The complete exception mapping.</returns>
    public KyrolusExceptionMapping TranslateToMapping(Exception exception, KyrolusErrorContext? context = null, bool? includeDetails = null)
    {
        var resolvedContext = context ?? CreateDefaultContext();
        
        // 1. Map exception to raw mapping (pure classification & envelope creation)
        var mapping = mappingService.Map(exception, resolvedContext);

        // 2. Localize human-readable title and detail according to culture
        var localizedEnvelope = KyrolusExceptionEnrichmentHelper.LocalizeEnvelope(localizer, mapping.Error, resolvedContext.Culture);
        mapping = mapping with { Error = localizedEnvelope };

        // 3. Enrich distributed tracing Activity (OpenTelemetry)
        var includeDetailsResolved = includeDetails ?? KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, environment);
        KyrolusExceptionActivityEnricher.Enrich(Activity.Current, mapping, resolvedContext, exception, includeDetailsResolved);

        // 4. Enrich diagnostic and request context metadata
        var enrichedMetadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(mapping.Error.Metadata, exception, resolvedContext, options, includeDetailsResolved);

        // 5. Sanitize sensitive metadata (Security Gatekeeper)
        var sanitizedMetadata = enrichedMetadata.Count > 0
            ? metadataSanitizer.Sanitize(enrichedMetadata, resolvedContext)
            : (mapping.Error.Metadata is null && enrichedMetadata.Count == 0 ? null : enrichedMetadata);

        // 6. Return finalized mapping
        var envelope = mapping.Error with { Metadata = sanitizedMetadata };
        return mapping with { Error = envelope };
    }

    /// <summary>
    /// Translates an exception into a <see cref="KyrolusErrorResult"/> suitable for serialization or background processing.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <param name="includeDetails">Explicit override for whether to include stack traces.</param>
    /// <returns>The resolved error result.</returns>
    public KyrolusErrorResult Translate(Exception exception, KyrolusErrorContext? context = null, bool? includeDetails = null)
    {
        var resolvedContext = context ?? CreateDefaultContext();
        var mapping = TranslateToMapping(exception, resolvedContext, includeDetails);
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
