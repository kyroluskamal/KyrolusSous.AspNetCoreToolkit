namespace KyrolusSous.Validation.FluentValidation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFluentValidation(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidator<>), typeof(FluentValidationRequestValidator<>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidatorWithContext<>), typeof(FluentValidationRequestValidator<>)));
        return services;
    }
}
