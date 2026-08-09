using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusTenantDataProtectionProvider(
    IDataProtectionProvider provider,
    IOptions<KyrolusDataProtectionTenantOptions> options)
    : IKyrolusTenantDataProtectionProvider
{
    private readonly IDataProtectionProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly KyrolusDataProtectionTenantOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public IDataProtector CreateProtector(string tenantId, string purpose)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));

        var baseProtector = provider.CreateProtector(
            options.UseTenantPrefix
                ? $"{options.PurposePrefix}:{tenantId}"
                : tenantId);
        return baseProtector.CreateProtector(purpose);
    }
}
