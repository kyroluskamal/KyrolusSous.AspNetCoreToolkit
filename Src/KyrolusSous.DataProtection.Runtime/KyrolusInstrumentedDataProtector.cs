using System.Diagnostics;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Runtime;

internal sealed class KyrolusInstrumentedDataProtector(
    IDataProtector inner,
    KyrolusDataProtectionInstrumentation instrumentation)
    : IDataProtector
{
    private readonly IDataProtector inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly KyrolusDataProtectionInstrumentation instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));

    public IDataProtector CreateProtector(string purpose)
        => new KyrolusInstrumentedDataProtector(inner.CreateProtector(purpose), instrumentation);

    public byte[] Protect(byte[] plaintext)
        => Execute("protect", () => inner.Protect(plaintext));

    public byte[] Unprotect(byte[] protectedData)
        => Execute("unprotect", () => inner.Unprotect(protectedData));

    private byte[] Execute(string operation, Func<byte[]> action)
    {
        using var activity = instrumentation.StartActivity(operation);
        var start = Stopwatch.GetTimestamp();

        try
        {
            var result = action();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            instrumentation.RecordSuccess(operation, elapsed);
            return result;
        }
        catch
        {
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            instrumentation.RecordFailure(operation, elapsed);
            throw;
        }
    }
}
