using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Validation.FluentValidation.Scanning;

/// <summary>
/// Reflection-based assembly scanning for FluentValidation <c>AbstractValidator&lt;T&gt;</c> classes, split out
/// from <c>KyrolusSous.Validation.FluentValidation</c> into its own package specifically so consumers who don't
/// need scanning (or need Native AOT/trimming) can reference the core adapter without pulling in
/// <see cref="RequiresUnreferencedCodeAttribute"/>-marked reflection code.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>KyrolusSous.Validation.FluentValidation</c> adapter (via <c>AddKyrolusFluentValidation()</c>)
    /// and then scans <paramref name="assemblies"/> for every FluentValidation <c>AbstractValidator&lt;T&gt;</c>
    /// (via FluentValidation's own <c>AddValidatorsFromAssemblies</c>), registering each with the container.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationRuntime();
    /// builder.Services.AddKyrolusFluentValidationScanning(Assembly.GetExecutingAssembly());
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="assemblies">The assemblies to scan for <c>AbstractValidator&lt;T&gt;</c> implementations.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    public static IServiceCollection AddKyrolusFluentValidationScanning(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.AddKyrolusFluentValidation();
        services.AddValidatorsFromAssemblies(assemblies);
        return services;
    }

    /// <summary>
    /// Convenience overload of <see cref="AddKyrolusFluentValidationScanning"/> that scans only the assembly
    /// containing <typeparamref name="T"/> - typically a marker type in the same project as your validators, so
    /// you don't have to spell out <c>Assembly.GetExecutingAssembly()</c> at the call site.
    /// </summary>
    /// <typeparam name="T">Any type declared in the assembly to scan.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    public static IServiceCollection AddKyrolusFluentValidationScanningFromAssemblyContaining<T>(
        this IServiceCollection services)
    {
        return services.AddKyrolusFluentValidationScanning(typeof(T).Assembly);
    }
}
