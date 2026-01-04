using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusInstrumentedDataProtectorFactory(
    IDataProtectionProvider provider,
    KyrolusDataProtectionInstrumentation instrumentation)
    : IKyrolusDataProtectorFactory
{
    private readonly IDataProtectionProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly KyrolusDataProtectionInstrumentation instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));

    public IDataProtector CreateProtector(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));
        var protector = provider.CreateProtector(purpose);
        return new KyrolusInstrumentedDataProtector(protector, instrumentation);
    }
}
