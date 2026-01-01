namespace KyrolusSous.CQRS.Marten.Config;

public static class MediatorExtensions
{
    public static IServiceCollection AddKyrolusCqrsMarten(this IServiceCollection services, params Assembly[] assemblies)
    {
        return services;
    }
}
