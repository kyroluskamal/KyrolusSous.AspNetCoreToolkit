

namespace KyrolusSous.CQRS.Base.Config;

public static class MediatorExtensions
{
    public static void AddSourceMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.TryAddScoped<ISourceSender, SourceSender>();

        services.AddValidatorsFromAssemblies(assemblies);
    }


}
