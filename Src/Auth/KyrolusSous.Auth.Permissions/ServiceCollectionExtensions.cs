using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Permissions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus permission-based authorization services and the default claim-based permission resolver.
    /// </summary>
    public static IServiceCollection AddKyrolusPermissions(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusPermissionResolver, KyrolusClaimPermissionResolver>();
        services.AddScoped<IAuthorizationHandler, KyrolusPermissionAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// Registers a custom permission resolver (e.g. database-backed, distributed cache-backed).
    /// </summary>
    public static IServiceCollection AddKyrolusPermissionResolver<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResolver>(this IServiceCollection services)
        where TResolver : class, IKyrolusPermissionResolver
    {
        services.Replace(ServiceDescriptor.Scoped<IKyrolusPermissionResolver, TResolver>());
        return services;
    }
}
