namespace KyrolusSous.CQRS.Validation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsValidation(
        this IServiceCollection services,
        Action<KyrolusValidationBehaviorOptions>? configure = null)
    {
        var options = new KyrolusValidationBehaviorOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusValidationBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusBatchValidationBehavior<,>)));
        return services;
    }
}
