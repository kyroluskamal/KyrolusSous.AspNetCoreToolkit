using KyrolusSous.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Validation.FluentValidation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFluentValidation(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidator<>), typeof(FluentValidationRequestValidator<>)));
        return services;
    }
}
