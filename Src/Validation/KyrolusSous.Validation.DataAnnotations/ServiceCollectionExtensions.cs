using KyrolusSous.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Validation.DataAnnotations;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DataAnnotationsRequestValidator{TRequest}"/> as an <em>open generic</em>, so every
    /// request type gets DataAnnotations validation automatically - no per-type registration needed, unlike a
    /// hand-written or Fluent validator, which each need their own <c>services.AddScoped&lt;IKyrolusRequestValidator&lt;T&gt;, ...&gt;()</c>
    /// line. Registered for both <see cref="IKyrolusRequestValidator{TRequest}"/> and
    /// <see cref="IKyrolusRequestValidatorWithContext{TRequest}"/> so the engine picks the context-aware overload.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationRuntime();
    /// builder.Services.AddKyrolusDataAnnotationsValidation();
    /// // Any request type with DataAnnotations attributes now validates with zero further registration:
    /// var failures = await engine.ValidateAsync(new CreateUserRequest { Email = "" });
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddKyrolusDataAnnotationsValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidator<>), typeof(DataAnnotationsRequestValidator<>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidatorWithContext<>), typeof(DataAnnotationsRequestValidator<>)));
        return services;
    }
}
