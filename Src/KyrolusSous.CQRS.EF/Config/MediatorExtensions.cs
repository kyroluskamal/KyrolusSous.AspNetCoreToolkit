
using KyrolusSous.CQRS.EF.VlidationBehaviour;

namespace KyrolusSous.CQRS.EF.Config;

public static class MediatorExtensions
{
    public static IServiceCollection AddKyrolusCqrsEf(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddValidatorsFromAssemblies(assemblies);
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(ValidationBehaviour<,>)));
        return services;
    }
}
