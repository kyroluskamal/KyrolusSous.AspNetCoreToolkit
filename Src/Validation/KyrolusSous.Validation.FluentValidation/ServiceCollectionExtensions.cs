namespace KyrolusSous.Validation.FluentValidation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFluentValidation(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidator<>), typeof(FluentValidationRequestValidator<>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidatorWithContext<>), typeof(FluentValidationRequestValidator<>)));
        return services;
    }

    public static IServiceCollection AddKyrolusFluentValidationFromAssemblyContaining<T>(this IServiceCollection services)
    {
        return services.AddKyrolusFluentValidationFromAssemblies(typeof(T).Assembly);
    }

    public static IServiceCollection AddKyrolusFluentValidationFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddKyrolusFluentValidation();
        foreach (var result in AssemblyScanner.FindValidatorsInAssemblies(assemblies))
        {
            services.TryAddTransient(result.InterfaceType, result.ValidatorType);
        }
        return services;
    }
}
