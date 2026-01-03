using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using KyrolusSous.Validation.FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Validation.FluentValidation.Scanning;

public static class ServiceCollectionExtensions
{
    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    public static IServiceCollection AddKyrolusFluentValidationScanning(
        this IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        services.AddKyrolusFluentValidation();
        services.AddValidatorsFromAssemblies(assemblies);
        return services;
    }
}
