namespace KyrolusSous.ExceptionHandling.Runtime.Helpers;

public static class KyrolusExceptionEnrichmentHelper
{
    public static bool ShouldIncludeDetails(KyrolusExceptionHandlingOptions options, IHostEnvironment environment)
    {
        if (options.IncludeExceptionDetailsInResponse)
            return true;

        return environment.IsDevelopment() && options.IncludeExceptionDetailsInDevelopment;
    }

    public static KyrolusExceptionMapping ApplyExceptionDetails(
        KyrolusExceptionMapping mapping,
        Exception exception,
        KyrolusErrorContext context,
        KyrolusExceptionHandlingOptions options,
        IKyrolusErrorMetadataSanitizer metadataSanitizer,
        bool includeDetails)
    {
        var metadata = mapping.Error.Metadata is not null
            ? new Dictionary<string, object?>(mapping.Error.Metadata, StringComparer.OrdinalIgnoreCase)
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

        if (metadata.Count == 0)
            return mapping;

        var sanitized = metadataSanitizer.Sanitize(metadata, context);
        var envelope = mapping.Error with { Metadata = sanitized };
        return mapping with { Error = envelope };
    }
}
