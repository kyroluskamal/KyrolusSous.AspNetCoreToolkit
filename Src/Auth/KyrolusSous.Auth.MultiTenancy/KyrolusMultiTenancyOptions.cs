namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Configuration options for multi-tenant resolution strategy and header spoofing defense.
/// </summary>
public sealed class KyrolusMultiTenancyOptions
{
    /// <summary>
    /// Gets or sets whether client HTTP headers (e.g. <c>X-Tenant-Id</c>) are trusted to resolve tenant identity.
    /// Defaults to <c>false</c> to defend against tenant spoofing from untrusted public clients.
    /// Enable only in secure internal microservice topologies or server-to-server calls.
    /// </summary>
    public bool AllowHeaderResolution { get; set; } = false;

    /// <summary>
    /// Gets or sets the custom header name inspected when <see cref="AllowHeaderResolution"/> is enabled.
    /// Defaults to <c>"X-Tenant-Id"</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// Gets or sets the claim type used to resolve tenant identity from authenticated JWT tokens.
    /// Defaults to <c>"tenant_id"</c>.
    /// </summary>
    public string ClaimType { get; set; } = "tenant_id";
}
