using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Abstractions;

public interface IKyrolusTenantDataProtectionProvider
{
    IDataProtector CreateProtector(string tenantId, string purpose);
}
