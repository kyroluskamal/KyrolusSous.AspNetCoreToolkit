namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorConfigurationTests
{
    private static ServiceCollection Scanned(Action<KyrolusMediatorConfiguration>? configure = null)
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

    #region Register Services From Assembly 
    [Fact(DisplayName = "RegisterServicesFromAssembly should throw ArgumentNullException when given null assembly")]
    public void RegisterServicesFromAssembly_should_throw_when_given_null_assembly()
    {
        var configuration = new KyrolusMediatorConfiguration();
        var exception = Should.Throw<ArgumentNullException>(() => configuration.RegisterServicesFromAssembly(null!));
        exception.ParamName.ShouldBe("assembly");
    }

    [Fact(DisplayName = "RegisterServicesFromAssemblyContaining should throw when given a null type")]
    public void RegisterServicesFromAssemblyContaining_should_throw_when_given_a_null_type()
    {
        var configuration = new KyrolusMediatorConfiguration();
        var exception = Should.Throw<ArgumentNullException>(() => configuration.RegisterServicesFromAssemblyContaining(null!));
        exception.ParamName.ShouldBe("type");
    }

    [Fact(DisplayName = "Registering the same assembly twice in configuration is idempotent")]
    public void Registering_the_same_assembly_twice_is_idempotent()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.RegisterServicesFromAssemblyContaining<Ping>();
        configuration.RegisterServicesFromAssemblyContaining<Ambiguous>(); // same assembly

        configuration.AssembliesToScan.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "Non generic RegisterServicesFromAssemblyContaining adds the assembly to the list of assemblies to scan")]
    public void RegisterServicesFromAssemblyContaining_adds_the_assembly_to_the_list_of_assemblies_to_scan()
    {
        var configuration = new KyrolusMediatorConfiguration();
        configuration.RegisterServicesFromAssemblyContaining(typeof(Ping));
        configuration.AssembliesToScan.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "RegisterServicesFromAssemblies should throw when given a null array")]
    public void RegisterServicesFromAssemblies_should_throw_when_given_a_null_array()
    {
        var configuration = new KyrolusMediatorConfiguration();
        var exception = Should.Throw<ArgumentNullException>(() => configuration.RegisterServicesFromAssemblies(null!));
        exception.ParamName.ShouldBe("assemblies");
    }

    [Fact(DisplayName = "RegisterServicesFromAssemblies should throw when given an empty array")]
    public void RegisterServicesFromAssemblies_should_throw_when_given_an_empty_array()
    {
        var configuration = new KyrolusMediatorConfiguration();
        var exception = Should.Throw<ArgumentException>(() => configuration.RegisterServicesFromAssemblies());
        exception.ParamName.ShouldBe("assemblies");
    }

    [Fact(DisplayName = "RegisterServicesFromAssemblies adds all assemblies to the list of assemblies to scan")]
    public void RegisterServicesFromAssemblies_adds_all_assemblies_to_the_list_of_assemblies_to_scan()
    {
        var configuration = new KyrolusMediatorConfiguration();
        configuration.RegisterServicesFromAssemblies(typeof(Ping).Assembly, typeof(IKyrolusMediator).Assembly);
        configuration.AssembliesToScan.Count.ShouldBe(2);
    }
    #endregion

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

    [Fact(DisplayName = "Mediator lifetime defaults to Scoped when not specified")]
    public void Mediator_lifetime_defaults_to_scoped()
    {
        var services = Scanned();
        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusMediator));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    #region AddOpenBehavior
    [Fact(DisplayName = "AddOpenBehavior should throw ArgumentNullException when given null openBehaviorType")]
    public void AddOpenBehavior_should_throw_when_given_null_openBehaviorType()
    {
        var configuration = new KyrolusMediatorConfiguration();
        var exception = Should.Throw<ArgumentNullException>(() => configuration.AddOpenBehavior(null!));
        exception.ParamName.ShouldBe("openBehaviorType");
    }

    [Fact(DisplayName = "AddOpenBehavior should throw when rejecting a closed behavior type")]
    public void AddOpenBehavior_should_throw_when_rejecting_a_closed_behavior_type()
    {
        var exception = Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
                   .AddOpenBehavior(typeof(ShortCircuitBehavior)));
        exception.Message.ShouldContain($"[KyrolusMediator] {typeof(ShortCircuitBehavior).FullName} is not an open generic type. Use AddBehavior for closed types.");
        exception.ParamName.ShouldBe("openBehaviorType");
    }

    [Fact(DisplayName = "AddOpenBehavior should throw when rejecting a type that does not implement IPipelineBehavior")]
    public void AddOpenBehavior_should_throw_when_rejecting_a_type_that_does_not_implement_IPipelineBehavior()
    {
        var exception = Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
       .AddOpenBehavior(typeof(List<>)));
        exception.Message.ShouldContain($"[KyrolusMediator] {typeof(List<>).FullName} implements neither IKyrolusPipelineBehavior<,> nor IKyrolusStreamPipelineBehavior<,>.");
        exception.ParamName.ShouldBe("openBehaviorType");
    }

    [Fact(DisplayName = "AddOpenBehavior should register open pipeline behavior implementing IKyrolusPipelineBehavior")]
    public void AddOpenBehavior_should_register_open_pipeline_behavior_implementing_IKyrolusPipelineBehavior()
    {
        var configuration = new KyrolusMediatorConfiguration();
        configuration.AddOpenBehavior(typeof(OpenPipelineBehavior<,>));

        configuration.OpenBehaviors.ShouldHaveSingleItem();
        configuration.OpenBehaviors[0].Implementation.ShouldBe(typeof(OpenPipelineBehavior<,>));
    }

    [Fact(DisplayName = "AddOpenBehavior registers open stream behavior implementing IKyrolusStreamPipelineBehavior")]
    public void AddOpenBehavior_should_register_open_stream_behavior_implementing_IKyrolusStreamPipelineBehavior()
    {
        var configuration = new KyrolusMediatorConfiguration();
        configuration.AddOpenBehavior(typeof(OpenStreamBehavior<,>));

        configuration.OpenStreamBehaviors.ShouldHaveSingleItem();
        configuration.OpenStreamBehaviors[0].Implementation.ShouldBe(typeof(OpenStreamBehavior<,>));
    }
    #endregion

    #region AddBehavior
    [Fact(DisplayName = "AddBehavior should throw ArgumentNullException when given null implementationType")]
    public void AddBehavior_should_throw_when_given_null_implementationType()
    {
        var configuration = new KyrolusMediatorConfiguration();
        var exception = Should.Throw<ArgumentNullException>(() => configuration.AddBehavior((Type)null!));
        exception.ParamName.ShouldBe("implementationType");
    }

    [Fact(DisplayName = "AddBehavior should throw when rejecting a type that does not implement IPipelineBehavior")]
    public void AddBehavior_should_throw_when_rejecting_a_type_that_does_not_implement_IPipelineBehavior()
    {
        var exception = Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddBehavior<KyrolusMediatorConfigurationTests>());
        exception.Message.ShouldContain($"[KyrolusMediator] {typeof(KyrolusMediatorConfigurationTests).FullName} implements neither IKyrolusPipelineBehavior<,> nor IKyrolusStreamPipelineBehavior<,>.");
        exception.ParamName.ShouldBe("implementationType");
    }

    [Fact(DisplayName = "AddBehavior should register closed pipeline behavior interface correctly skipping non-generic interfaces")]
    public void AddBehavior_should_register_closed_pipeline_behavior_and_skips_non_generic_interfaces()
    {
        var configuration = new KyrolusMediatorConfiguration();
        configuration.AddBehavior<ClosedBehaviorWithNonGenericInterface>();

        configuration.ClosedBehaviors.ShouldHaveSingleItem();
        configuration.ClosedBehaviors[0].Service.ShouldBe(typeof(IKyrolusPipelineBehavior<Ping, string>));
    }

    [Fact(DisplayName = "AddBehavior should register closed stream behavior interface correctly")]
    public void AddBehavior_should_register_closed_stream_behavior()
    {
        var configuration = new KyrolusMediatorConfiguration();
        configuration.AddBehavior<ClosedStreamBehavior>();

        configuration.ClosedStreamBehaviors.ShouldHaveSingleItem();
        configuration.ClosedStreamBehaviors[0].Service.ShouldBe(typeof(IKyrolusStreamPipelineBehavior<CountTo, int>));
    }
    #endregion
}

public sealed class OpenPipelineBehavior<TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>, IDisposable
    where TRequest : IKyrolusRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next(cancellationToken);
    public void Dispose() { }
}

public sealed class ClosedBehaviorWithNonGenericInterface : IKyrolusPipelineBehavior<Ping, string>, IDisposable
{
    public Task<string> Handle(Ping request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken) => next(cancellationToken);
    public void Dispose() { }
}

public sealed class ClosedStreamBehavior : IKyrolusStreamPipelineBehavior<CountTo, int>
{
    public IAsyncEnumerable<int> Handle(CountTo request, StreamHandlerDelegate<int> next, CancellationToken cancellationToken) => next(cancellationToken);
}

public sealed class OpenStreamBehavior<TRequest, TResponse> : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IKyrolusStreamRequest<TResponse>
{
    public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next(cancellationToken);
}
