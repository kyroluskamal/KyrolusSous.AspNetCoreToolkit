namespace KyrolusSous.ExceptionHandling.Runtime.Helpers;

/// <summary>
/// Provides helper methods for evaluating exception detail inclusion and enriching diagnostic metadata.
/// </summary>
public static class KyrolusExceptionEnrichmentHelper
{
    /// <summary>
    /// Determines whether exception diagnostic details (e.g. stack traces) should be included in the response.
    /// </summary>
    /// <param name="options">The exception handling options.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns><c>true</c> if details should be included; otherwise, <c>false</c>.</returns>
    public static bool ShouldIncludeDetails(KyrolusExceptionHandlingOptions options, IHostEnvironment environment)
        => options.IncludeExceptionDetailsInResponse
            || (options.IncludeExceptionDetailsInDevelopment && environment.IsDevelopment());

    /// <summary>
    /// Enriches exception metadata with ambient request context claims and diagnostic exception details.
    /// </summary>
    /// <param name="baseMetadata">The initial metadata dictionary extracted from the exception.</param>
    /// <param name="exception">The caught exception.</param>
    /// <param name="context">Ambient request context.</param>
    /// <param name="options">Exception handling options.</param>
    /// <param name="includeDetails">Whether to append stack traces and exception types.</param>
    /// <returns>The enriched metadata dictionary.</returns>
    public static IReadOnlyDictionary<string, object?> EnrichMetadata(
        IReadOnlyDictionary<string, object?>? baseMetadata,
        Exception exception,
        KyrolusErrorContext context,
        KyrolusExceptionHandlingOptions options,
        bool includeDetails)
    {
        var metadata = baseMetadata is not null
            ? new Dictionary<string, object?>(baseMetadata, StringComparer.OrdinalIgnoreCase)
            : [];

        if (options.IncludeContextMetadata)
        {
            if (!string.IsNullOrWhiteSpace(context.CorrelationId))
                metadata["correlationId"] = context.CorrelationId;

            if (!string.IsNullOrWhiteSpace(context.UserId))
                metadata["userId"] = context.UserId;

            if (!string.IsNullOrWhiteSpace(context.TenantId))
                metadata["tenantId"] = context.TenantId;
        }

        if (includeDetails)
        {
            metadata["exceptionType"] = exception.GetType().FullName;
            metadata["stackTrace"] = exception.StackTrace;

            if (exception.InnerException is not null)
                metadata["innerException"] = exception.InnerException.Message;
        }

        return metadata;
    }
}
