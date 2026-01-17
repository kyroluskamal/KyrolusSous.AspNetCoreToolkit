namespace KyrolusSous.EndpointKit.Marten.Config;

public sealed class KyrolusEndpointKitOptions
{
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";
    public string? TenantClaimType { get; set; } = "tenant_id";
    public string? ScopeHeaderName { get; set; } = "X-Scope";
    public string? ScopeClaimType { get; set; } = "scope";
}
