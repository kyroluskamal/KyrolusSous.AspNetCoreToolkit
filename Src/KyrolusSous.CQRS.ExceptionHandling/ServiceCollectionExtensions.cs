namespace KyrolusSous.CQRS.ExceptionHandling;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCqrsExceptionHandling(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusExceptionMappingBehavior<,>)));
        return services;
    }
}
