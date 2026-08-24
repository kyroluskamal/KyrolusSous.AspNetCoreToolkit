namespace KyrolusSous.CQRS.Mapping;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsMapping(
        this IServiceCollection services,
        Func<IServiceProvider, IKyrolusObjectMapper> factory)
    {
        services.TryAddSingleton(factory);
        return services;
    }

    public static IServiceCollection AddKyrolusCqrsMapping(
        this IServiceCollection services,
        IKyrolusObjectMapper mapper)
    {
        services.TryAddSingleton(mapper);
        return services;
    }
}
