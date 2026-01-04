using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.DataProtection.Ephemeral;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusEphemeralDataProtection(
        this KyrolusDataProtectionBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.DataProtection.UseEphemeralDataProtectionProvider();
        return builder;
    }
}
