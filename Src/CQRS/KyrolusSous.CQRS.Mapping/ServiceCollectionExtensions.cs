namespace KyrolusSous.CQRS.Mapping;

using KyrolusSous.CQRS.Mapping.Behaviors;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CQRS mapping pipeline behavior into the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusCqrsMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusMappingPipelineBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers a factory for <see cref="IKyrolusObjectMapper"/> and the CQRS mapping pipeline behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">The factory resolving the mapper instance from DI.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusCqrsMapping(
        this IServiceCollection services,
        Func<IServiceProvider, IKyrolusObjectMapper> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.TryAddSingleton<IKyrolusObjectMapper>(factory);
        return services.AddKyrolusCqrsMapping();
    }

    /// <summary>
    /// Registers a singleton instance of <see cref="IKyrolusObjectMapper"/> and the CQRS mapping pipeline behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="mapper">The concrete mapper instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusCqrsMapping(
        this IServiceCollection services,
        IKyrolusObjectMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mapper);

        services.TryAddSingleton<IKyrolusObjectMapper>(mapper);
        return services.AddKyrolusCqrsMapping();
    }
}
