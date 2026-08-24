namespace KyrolusSous.Mapping.Runtime;

/// <summary>
/// Provides Dependency Injection extensions for registering the KyrolusSous mapping subsystem.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IKyrolusObjectMapper"/> and its runtime configuration in the service collection.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional configuration action for configuring mapping profiles and rules.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddKyrolusMapping(
        this IServiceCollection services,
        Action<KyrolusMappingConfiguration>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = new KyrolusMappingConfiguration();
        configure?.Invoke(configuration);

        services.TryAddSingleton(configuration);
        services.TryAddSingleton<IKyrolusObjectMapper>(sp =>
        {
            var config = sp.GetRequiredService<KyrolusMappingConfiguration>();
            foreach (var profile in sp.GetServices<KyrolusMappingProfile>())
            {
                config.AddProfile(profile);
            }

            return new KyrolusObjectMapper(config);
        });

        return services;
    }

    /// <summary>
    /// Registers a specific <see cref="KyrolusMappingProfile"/> into the mapping subsystem.
    /// </summary>
    /// <typeparam name="TProfile">The mapping profile type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMappingProfile<TProfile>(this IServiceCollection services)
        where TProfile : KyrolusMappingProfile, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<KyrolusMappingProfile, TProfile>();
        return services;
    }

    /// <summary>
    /// Scans the given assembly for all concrete <see cref="KyrolusMappingProfile"/> types and registers them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMappingProfilesFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var profileTypes = assembly.GetTypes()
            .Where(t => typeof(KyrolusMappingProfile).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) is not null);

        foreach (var profileType in profileTypes)
        {
            var profile = (KyrolusMappingProfile)Activator.CreateInstance(profileType)!;
            services.AddSingleton(profile);
        }

        return services;
    }
}
