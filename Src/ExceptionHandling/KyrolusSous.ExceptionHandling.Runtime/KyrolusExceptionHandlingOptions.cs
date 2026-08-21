namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusExceptionHandlingOptions
{
    public bool IncludeExceptionDetailsInResponse { get; set; }
    public bool IncludeExceptionDetailsInDevelopment { get; set; } = true;
    public bool IncludeTraceId { get; set; } = true;
    public bool IncludeCorrelationId { get; set; } = true;
    public bool IncludeContextMetadata { get; set; } = true;
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";
    public string? UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;
    public string? TenantIdClaimType { get; set; } = "tenant_id";
    public bool LogHandledExceptions { get; set; }
    public bool LogUnhandledExceptions { get; set; } = true;

    public bool SanitizeMetadata { get; set; } = true;
    public HashSet<string> SensitiveMetadataKeys { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "secret",
        "token",
        "authorization",
        "cookie",
        "set-cookie",
        "api-key",
        "apikey",
        "access_token",
        "refresh_token",
        "jwt"
    };

    public HashSet<string>? MetadataAllowList { get; set; }
}
