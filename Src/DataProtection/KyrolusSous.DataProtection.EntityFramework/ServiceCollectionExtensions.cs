using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.DataProtection.EntityFramework;

public static class ServiceCollectionExtensions
{
    public static KyrolusDataProtectionBuilder AddKyrolusDataProtectionEntityFramework(
        this KyrolusDataProtectionBuilder builder,
        Action<DbContextOptionsBuilder> configureDb,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configureDb is null) throw new ArgumentNullException(nameof(configureDb));

        builder.Services.AddDbContext<KyrolusDataProtectionKeysContext>(
            configureDb,
            contextLifetime,
            contextLifetime);

        builder.DataProtection.PersistKeysToDbContext<KyrolusDataProtectionKeysContext>();
        return builder;
    }

    public static KyrolusDataProtectionBuilder PersistKeysToDbContext<TContext>(
        this KyrolusDataProtectionBuilder builder)
        where TContext : DbContext, IDataProtectionKeyContext
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.DataProtection.PersistKeysToDbContext<TContext>();
        return builder;
    }
}
