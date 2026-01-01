namespace KyrolusSous.CQRS.EF.Config;

public static class MediatorExtensions
{
    public static IServiceCollection AddKyrolusCqrsEf(this IServiceCollection services, params Assembly[] assemblies)
    {
        return services;
    }
}
