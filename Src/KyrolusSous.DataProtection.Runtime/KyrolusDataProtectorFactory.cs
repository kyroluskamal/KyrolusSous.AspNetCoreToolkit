using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectorFactory(IDataProtectionProvider provider) : IKyrolusDataProtectorFactory
{
    private readonly IDataProtectionProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public IDataProtector CreateProtector(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));
        return provider.CreateProtector(purpose);
    }
}
