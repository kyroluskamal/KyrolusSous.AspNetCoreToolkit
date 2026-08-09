namespace KyrolusSous.Mediator.Runtime.UnitTests;

public static class MediatorRuntimeTestsHelper
{
    public static ServiceCollection Scanned(Action<KyrolusMediatorConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Recorder());
        services.AddKyrolusMediator(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<Ping>();
            configuration.ThrowOnDuplicateRequestHandlers = false;
            configure?.Invoke(configuration);
        });

        services.AddKyrolusMediatorReflection();
        return services;
    }
}
