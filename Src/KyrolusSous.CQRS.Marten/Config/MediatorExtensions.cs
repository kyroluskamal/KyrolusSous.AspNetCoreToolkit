
using KyrolusSous.CQRS.Marten.VlidationBehaviour;

namespace KyrolusSous.CQRS.Marten.Config;

public static class MediatorExtensions
{
    public static IServiceCollection AddKyrolusCqrsMarten(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddValidatorsFromAssemblies(assemblies);
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(ValidationBehaviour<,>)));
        return services;
    }
}
