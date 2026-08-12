namespace KyrolusSous.Validation.Runtime.UnitTests;

public static class TestHelper
{
    public static ServiceProvider BuildServiceProviderWithValidationRuntime(params Action<IServiceCollection>[] configureServices)
    {
        var services = new ServiceCollection();
        services.AddKyrolusValidationRuntime();

        foreach (var configure in configureServices)
        {
            configure?.Invoke(services);
        }

        return services.BuildServiceProvider();
    }

    public static ServiceProvider BuildServiceProviderWithValidationRuntime(IEnumerable<IServiceCollection>? servicesToAdd = null)
    {
        var services = new ServiceCollection();
        services.AddKyrolusValidationRuntime();

        if (servicesToAdd is not null)
        {
            foreach (var collection in servicesToAdd)
            {
                foreach (var descriptor in collection)
                {
                    ((ICollection<ServiceDescriptor>)services).Add(descriptor);
                }
            }
        }

        return services.BuildServiceProvider();
    }
    public static void AddsAllRequiredServices(ServiceProvider serviceProvider)
    {
   
        // Assert
        var validatorEngine = serviceProvider.GetService<IKyrolusValidationEngine>();
        validatorEngine.ShouldNotBeNull();
        validatorEngine.ShouldBeOfType<KyrolusValidationEngine>();

        var profileProvider = serviceProvider.GetService<IKyrolusValidationProfileProvider>();
        profileProvider.ShouldNotBeNull();
        profileProvider.ShouldBeOfType<KyrolusValidationProfileProvider>();
        var cacheStore = serviceProvider.GetService<IKyrolusValidationCacheStore>();
        cacheStore.ShouldNotBeNull();
        cacheStore.ShouldBeOfType<KyrolusValidationMemoryCacheStore>();
        var cacheKeyProvider = serviceProvider.GetService<IKyrolusValidationCacheKeyProvider>();
        cacheKeyProvider.ShouldNotBeNull();
        cacheKeyProvider.ShouldBeOfType<KyrolusValidationCacheKeyProvider>();
        var metrics = serviceProvider.GetService<IKyrolusValidationMetrics>();
        metrics.ShouldNotBeNull();
        metrics.ShouldBeOfType<KyrolusNoopValidationMetrics>();
        var tracer = serviceProvider.GetService<IKyrolusValidationTracer>();
        tracer.ShouldNotBeNull();
        tracer.ShouldBeOfType<KyrolusNoopValidationTracer>();
        var hooks = serviceProvider.GetServices<IKyrolusValidationHook>();
        hooks.ShouldNotBeNull();
        hooks.ShouldContain(h => h is KyrolusValidationMetricsHook);
        hooks.ShouldContain(h => h is KyrolusValidationTracingHook);
    }
}