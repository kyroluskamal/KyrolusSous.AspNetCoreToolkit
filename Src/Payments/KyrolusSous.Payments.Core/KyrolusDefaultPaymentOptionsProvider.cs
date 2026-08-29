using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultPaymentOptionsProvider<TOptions>(IOptions<TOptions> options)
    : IKyrolusPaymentOptionsProvider<TOptions> where TOptions : class
{
    public ValueTask<TOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}
