using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.DataProtection.Abstractions;

public sealed class KyrolusDataProtectionBuilder(
    IServiceCollection services,
    IDataProtectionBuilder dataProtection,
    KyrolusDataProtectionOptions options)
{

    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
    public IDataProtectionBuilder DataProtection { get; } = dataProtection ?? throw new ArgumentNullException(nameof(dataProtection));
    public KyrolusDataProtectionOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));
}
