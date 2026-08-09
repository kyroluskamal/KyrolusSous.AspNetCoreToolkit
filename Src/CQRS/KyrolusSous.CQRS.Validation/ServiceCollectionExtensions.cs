namespace KyrolusSous.CQRS.Validation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsValidation(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusValidationBehavior<,>)));
        return services;
    }
}
