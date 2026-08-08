namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorConfigurationTests
{
    private static IServiceCollection Scanned(Action<KyrolusMediatorConfiguration>? configure = null)
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

    [Fact(DisplayName = "Registering the same assembly twice in configuration is idempotent")]
    public void Registering_the_same_assembly_twice_is_idempotent()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.RegisterServicesFromAssemblyContaining<Ping>();
        configuration.RegisterServicesFromAssemblyContaining<Ambiguous>(); // same assembly

        configuration.AssembliesToScan.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Handler lifetime can be configured via KyrolusMediatorConfiguration")]
    public void Handler_lifetime_is_configurable()
    {
        var services = Scanned(configuration => configuration.Lifetime = ServiceLifetime.Scoped);

        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusQueryHandler<Ping, string>));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact(DisplayName = "Handler lifetime defaults to Transient when not specified")]
    public void Handler_lifetime_defaults_to_transient()
    {
        var services = Scanned();

        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusQueryHandler<Ping, string>));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact(DisplayName = "Mediator lifetime can be configured via KyrolusMediatorConfiguration")]
    public void Mediator_lifetime_is_configurable()
    {
        var services = Scanned(configuration => configuration.MediatorLifetime = ServiceLifetime.Singleton);

        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusMediator));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact(DisplayName = "AddOpenBehavior rejects a closed behavior type")]
    public void AddOpenBehavior_rejects_a_closed_type()
        => Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddOpenBehavior(typeof(ShortCircuitBehavior)));

    [Fact(DisplayName = "AddOpenBehavior rejects a type that does not implement IPipelineBehavior")]
    public void AddOpenBehavior_rejects_a_type_that_is_not_a_behavior()
        => Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddOpenBehavior(typeof(List<>)));

    [Fact(DisplayName = "AddBehavior rejects a type that does not implement IPipelineBehavior")]
    public void AddBehavior_rejects_a_type_that_is_not_a_behavior()
        => Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddBehavior<KyrolusMediatorConfigurationTests>());

    [Fact(DisplayName = "AddBehavior registers the closed behavior interface correctly")]
    public void AddBehavior_registers_the_closed_interface()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.AddBehavior<ShortCircuitBehavior>();

        configuration.ClosedBehaviors.ShouldHaveSingleItem();
        configuration.ClosedBehaviors[0].Service.ShouldBe(typeof(IKyrolusPipelineBehavior<Ping, string>));
    }
}
