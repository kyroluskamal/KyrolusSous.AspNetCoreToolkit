global using Microsoft.AspNetCore.DataProtection;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Dataprotection.Config;

public static class DataprotectionExtension
{
    public static IServiceCollection AddDataProtectionKeysContext(this IServiceCollection services,
    Action<DbContextOptionsBuilder>? optionsAction = null, string appName = "default")
    {
        services.AddDbContext<DataProtectionKeysContext>(optionsBuilder =>
        {
            optionsAction?.Invoke(optionsBuilder);
            // Add additional options here
        });

        services.AddDataProtection()
                .PersistKeysToDbContext<DataProtectionKeysContext>()
                .SetApplicationName(appName);

        return services;
    }
}
