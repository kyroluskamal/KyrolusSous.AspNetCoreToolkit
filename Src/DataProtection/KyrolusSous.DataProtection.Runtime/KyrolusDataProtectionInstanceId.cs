using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionInstanceId
{
    public KyrolusDataProtectionInstanceId(
        IOptionsMonitor<KyrolusDataProtectionKeyRingRefreshOptions> options)
    {
        var value = options?.CurrentValue.InstanceId;
        Value = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
    }

    public string Value { get; }
}
