namespace KyrolusSous.Validation.FluentValidation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FluentValidationRequestValidator{TRequest}"/> as an open generic adapter, so any
    /// request type with a FluentValidation <c>AbstractValidator&lt;T&gt;</c> registered under FluentValidation's
    /// own <c>IValidator&lt;T&gt;</c> also becomes available through <see cref="IKyrolusRequestValidator{TRequest}"/>.
    /// Does not register the FluentValidation validators themselves - register each one manually
    /// (<c>services.AddScoped&lt;IValidator&lt;CreateUserRequest&gt;, CreateUserValidator&gt;()</c>), or use
    /// <c>KyrolusSous.Validation.FluentValidation.Scanning</c>'s <c>AddKyrolusFluentValidationScanning(...)</c>
    /// to discover them by reflection instead.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationRuntime();
    /// builder.Services.AddKyrolusFluentValidation();
    /// builder.Services.AddScoped&lt;IValidator&lt;CreateUserRequest&gt;, CreateUserValidator&gt;();
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddKyrolusFluentValidation(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidator<>), typeof(FluentValidationRequestValidator<>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IKyrolusRequestValidatorWithContext<>), typeof(FluentValidationRequestValidator<>)));
        return services;
    }
}
