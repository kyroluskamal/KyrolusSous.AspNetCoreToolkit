using KyrolusSous.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Validation.DataAnnotations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusDataAnnotationsValidation(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusRequestValidator<>), typeof(DataAnnotationsRequestValidator<>)));
        return services;
    }
}
