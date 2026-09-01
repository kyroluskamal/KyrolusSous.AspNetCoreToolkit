namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// DI registration entry points for the Validation runtime: the engine itself, its default (in-memory) cache
/// store, no-op observability defaults, and optional validator-registration strategies.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core validation runtime: <see cref="IKyrolusValidationEngine"/>, the default in-memory
    /// <see cref="IKyrolusValidationCacheStore"/>, the profile provider, and no-op Metrics/Tracing defaults. This
    /// is the one call every consumer needs regardless of which validator-writing style (Fluent, DataAnnotations,
    /// FluentValidation, hand-written) or registration strategy (manual, <see cref="AddKyrolusScannedValidators"/>,
    /// or the source-generated <c>AddKyrolusGeneratedValidators()</c>) they use for the validators themselves.
    /// Uses <c>TryAdd*</c> throughout, so calling it more than once (directly, or indirectly via
    /// <see cref="AddKyrolusValidationProfile"/>/<see cref="AddKyrolusValidationProfiles(IServiceCollection, KyrolusValidationProfile[])"/>) is safe and a no-op after the first call.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationRuntime();
    /// builder.Services.AddScoped&lt;IKyrolusRequestValidator&lt;CreateUserRequest&gt;, CreateUserValidator&gt;();
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddKyrolusValidationRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusValidationEngine, KyrolusValidationEngine>();
        services.TryAddSingleton<IKyrolusValidationProfileProvider, KyrolusValidationProfileProvider>();
        services.TryAddSingleton<IKyrolusValidationCacheStore, KyrolusValidationMemoryCacheStore>();
        services.TryAddSingleton<IKyrolusValidationCacheKeyProvider, KyrolusValidationCacheKeyProvider>();
        services.TryAddSingleton<IKyrolusValidationMetrics, KyrolusNoopValidationMetrics>();
        services.TryAddSingleton<IKyrolusValidationTracer, KyrolusNoopValidationTracer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusValidationHook, KyrolusValidationMetricsHook>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusValidationHook, KyrolusValidationTracingHook>());
        return services;
    }

    /// <summary>
    /// Registers a single named <see cref="KyrolusValidationProfile"/>, so it can be applied later via
    /// <see cref="KyrolusValidationContext.Profiles"/>. Also calls <see cref="AddKyrolusValidationRuntime"/>
    /// internally (idempotent via <c>TryAdd*</c>), so a consumer who only needs profiles can call this alone
    /// without a separate setup step.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationProfile(new KyrolusValidationProfile(
    ///     "Admin",
    ///     new KyrolusValidationContext(RuleSets: ["Admin", "Audit"])));
    ///
    /// // later, in application code:
    /// var context = new KyrolusValidationContext(Profiles: ["Admin"]);
    /// var failures = await engine.ValidateAsync(request, context);
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="profile">The profile to register.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddKyrolusValidationProfile(
        this IServiceCollection services,
        KyrolusValidationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        AddKyrolusValidationRuntime(services);
        services.AddSingleton(profile);
        return services;
    }

    /// <summary>
    /// Registers multiple named <see cref="KyrolusValidationProfile"/> instances in one call. See
    /// <see cref="AddKyrolusValidationProfile"/> for the single-profile overload and further remarks.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationProfiles(
    ///     KyrolusValidationProfiles.Create,
    ///     KyrolusValidationProfiles.Update);
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="profiles">The profiles to register. Must contain at least one entry.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="profiles"/> is empty.</exception>
    public static IServiceCollection AddKyrolusValidationProfiles(
        this IServiceCollection services,
        params KyrolusValidationProfile[] profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Length == 0) throw new ArgumentException("At least one profile must be provided.", nameof(profiles));

        return AddKyrolusValidationProfiles(services, (IEnumerable<KyrolusValidationProfile>)profiles);
    }

    /// <inheritdoc cref="AddKyrolusValidationProfiles(IServiceCollection, KyrolusValidationProfile[])" />
    public static IServiceCollection AddKyrolusValidationProfiles(
        this IServiceCollection services,
        IEnumerable<KyrolusValidationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (!profiles.Any()) throw new ArgumentException("At least one profile must be provided.", nameof(profiles));

        AddKyrolusValidationRuntime(services);

        foreach (var profile in profiles)
            if (profile is not null) services.AddSingleton(profile);

        return services;
    }

    // Validation.Runtime has no localization-specific registration of its own: KyrolusValidationEngine
    // takes IKyrolusLocalizer directly as an optional dependency. Register one via
    // KyrolusSous.Localization.Json's AddKyrolusJsonLocalization / AddKyrolusDictionaryLocalization, or
    // KyrolusSous.Localization.StringLocalizer's AddKyrolusStringLocalizerLocalization<TResource>.

    /// <summary>
    /// Reflection-scans the given assemblies for every concrete class implementing
    /// <see cref="IKyrolusRequestValidator{TRequest}"/> and registers each as a transient service for that
    /// closed interface. A pure registration step - it does <em>not</em> call
    /// <see cref="AddKyrolusValidationRuntime"/>, so call that separately (order doesn't matter, since
    /// registration here doesn't depend on the runtime being set up yet).
    /// </summary>
    /// <remarks>
    /// Not AOT/trimming-safe (see <see cref="RequiresUnreferencedCodeAttribute"/>). For an AOT-friendly
    /// equivalent that discovers validators at compile time instead, use
    /// <c>KyrolusSous.Validation.Generator</c>'s <c>AddKyrolusGeneratedValidators()</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddKyrolusValidationRuntime();
    /// builder.Services.AddKyrolusScannedValidators(Assembly.GetExecutingAssembly());
    /// </code>
    /// </example>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="assemblies">The assemblies to scan for validator implementations.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    public static IServiceCollection AddKyrolusScannedValidators(
        this IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        RegisterValidators(services, assemblies);
        return services;
    }

    /// <summary>
    /// Registers every concrete <see cref="IKyrolusRequestValidator{TRequest}"/> implementation found in
    /// <paramref name="assemblies"/> as a transient service, keyed by its closed generic interface. Uses
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
    /// so re-scanning the same assembly (or overlapping assemblies) never registers the same implementation twice.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection to scan for validators. This is not AOT-friendly.")]
    private static void RegisterValidators(IServiceCollection services, System.Reflection.Assembly[] assemblies)
    {
        if (assemblies.Length == 0) return;

        foreach (var type in assemblies.SelectMany(static assembly => assembly.GetTypes()).Where(IsConcreteType))
            foreach (var iface in GetValidatorInterfaces(type))
                services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
    }

    /// <summary>True for a class that can actually be instantiated (excludes abstract classes and interfaces).</summary>
    private static bool IsConcreteType(Type type) => !type.IsAbstract && !type.IsInterface;

    /// <summary>Yields every closed <c>IKyrolusRequestValidator&lt;T&gt;</c> interface <paramref name="type"/> implements (typically zero or one, but a type may validate more than one request type).</summary>
    private static IEnumerable<Type> GetValidatorInterfaces(Type type)
    {
        foreach (var iface in type.GetInterfaces())
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IKyrolusRequestValidator<>))
                yield return iface;
    }
}
