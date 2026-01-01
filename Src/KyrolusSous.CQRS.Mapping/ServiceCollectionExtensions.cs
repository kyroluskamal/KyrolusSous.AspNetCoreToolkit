namespace KyrolusSous.CQRS.Mapping;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsMapping(
        this IServiceCollection services,
        Func<IServiceProvider, IObjectMapper> factory)
    {
        services.TryAddSingleton(factory);
        return services;
    }

    public static IServiceCollection AddKyrolusCqrsMapping(
        this IServiceCollection services,
        IObjectMapper mapper)
    {
        services.TryAddSingleton(mapper);
        return services;
    }
}
