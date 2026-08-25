namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorExtensionsTests
{
    #region AddKyrolusMediatorSender
    [Fact(DisplayName = "AddKyrolusMediatorSender registers the sender and a placeholder dispatcher")]
    public void AddKyrolusMediatorSender_registers_sender_and_placeholder_dispatcher()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediatorSender();

        var senderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediatorSender));
        senderDescriptor.ShouldNotBeNull();
        senderDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        var dispatcherDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediatorDispatcher));
        dispatcherDescriptor.ShouldNotBeNull();
        dispatcherDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact(DisplayName = "AddKyrolusMediatorSender should throw InvalidOperationException when resolving IKyrolusMediatorDispatcher without a registered dispatcher")]
    public void AddKyrolusMediatorSender_should_throw_when_resolving_dispatcher_without_registered_dispatcher()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediatorSender();
        var serviceProvider = services.BuildServiceProvider();
        var exception = Should.Throw<InvalidOperationException>(() => serviceProvider.GetRequiredService<IKyrolusMediatorDispatcher>());
        exception.Message.ShouldContain("[KyrolusMediator] No dispatcher is registered. Reference KyrolusSous.Mediator.Generator " +
            "and call AddKyrolusMediatorGeneratedDispatcher(), or reference " +
            "KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection().");
    }
    #endregion

    #region AddKyrolusMediatorPublisher
    [Fact(DisplayName = "AddKyrolusMediatorPublisher registers the publisher and the default notification publish strategy")]
    public void AddKyrolusMediatorPublisher_registers_publisher_and_default_notification_strategy()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediatorPublisher();

        var publisherDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediatorPublisher));
        publisherDescriptor.ShouldNotBeNull();
        publisherDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        publisherDescriptor.ImplementationType.ShouldBe(typeof(KyrolusMediatorPublisher));
        var strategyDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusNotificationPublishStrategy));
        strategyDescriptor.ShouldNotBeNull();
        strategyDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        strategyDescriptor.ImplementationType.ShouldBe(typeof(KyrolusParallelNotificationPublishStrategy));
    }
    #endregion

    #region UseKyrolusMediator Notifications
    [Fact(DisplayName = "UseKyrolusMediatorSequentialNotifications replaces any existing notification publish sequence strategy with the sequential one")]
    public void UseKyrolusMediatorSequentialNotifications_replaces_existing_notification_strategy()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediatorPublisher();
        services.UseKyrolusMediatorSequentialNotifications();

        var strategyDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusNotificationPublishStrategy));
        strategyDescriptor.ShouldNotBeNull();
        strategyDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        strategyDescriptor.ImplementationType.ShouldBe(typeof(KyrolusSequentialNotificationPublishStrategy));
    }

    [Fact(DisplayName = "UseKyrolusMediatorParallelNotifications replaces any existing notification publish sequence strategy with the parallel one ")]
    public void UseKyrolusMediatorParallelNotifications_replaces_existing_notification_strategy()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediatorPublisher();
        services.UseKyrolusMediatorParallelNotifications();

        var strategyDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusNotificationPublishStrategy));
        strategyDescriptor.ShouldNotBeNull();
        strategyDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        strategyDescriptor.ImplementationType.ShouldBe(typeof(KyrolusParallelNotificationPublishStrategy));
    }
    #endregion

    #region AddKyrolusMediator
    [Fact(DisplayName = "AddKyrolusMediator throws ArgumentNullException when services is null")]
    public void AddKyrolusMediator_throws_ArgumentNullException_when_services_is_null()
    {
        var services = (IServiceCollection)null!;
        var exception = Should.Throw<ArgumentNullException>(() => services.AddKyrolusMediator(configuration => { }));
        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("services");
    }
    [Fact(DisplayName = "AddKyrolusMediator throws ArgumentNullException when configure is null")]
    public void AddKyrolusMediator_throws_ArgumentNullException_when_configure_is_null()
    {
        var services = new ServiceCollection();
        var exception = Should.Throw<ArgumentNullException>(() => services.AddKyrolusMediator(null!));
        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("configure");
    }
    [Fact(DisplayName = "AddKyrolusMediator registers the configuration")]
    public void AddKyrolusMediator_registers_configuration()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<Ping>();
            configuration.ThrowOnDuplicateRequestHandlers = false;
        });

        var configurationDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KyrolusMediatorConfiguration));
        configurationDescriptor.ShouldNotBeNull();
        configurationDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        configurationDescriptor.ImplementationInstance.ShouldBeOfType<KyrolusMediatorConfiguration>();
    }
    [Fact(DisplayName = "AddKyrolusMediator registers the sender and publisher")]
    public void AddKyrolusMediator_registers_sender_and_publisher()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(configuration => { });

        var senderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediatorSender));
        senderDescriptor.ShouldNotBeNull();
        senderDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        senderDescriptor.ImplementationType.ShouldBe(typeof(KyrolusMediatorSender));

        var publisherDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediatorPublisher));
        publisherDescriptor.ShouldNotBeNull();
        publisherDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        publisherDescriptor.ImplementationType.ShouldBe(typeof(KyrolusMediatorPublisher));
    }
    [Fact(DisplayName = "AddKyrolusMediator registers the mediator")]
    public void AddKyrolusMediator_registers_mediator()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(configuration => { });
        var mediatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediator));
        mediatorDescriptor.ShouldNotBeNull();
        mediatorDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        mediatorDescriptor.ImplementationType.ShouldBe(typeof(KyrolusMediator));

        var ImediaorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
        ImediaorDescriptor.ShouldNotBeNull();
        ImediaorDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        ImediaorDescriptor.ImplementationType.ShouldBe(typeof(KyrolusMediator));
    }

    [Fact(DisplayName = "AddKyrolusMediator registers the default notification publish strategy")]
    public void AddKyrolusMediator_registers_default_notification_strategy()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(configuration => { });

        var strategyDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusNotificationPublishStrategy));
        strategyDescriptor.ShouldNotBeNull();
        strategyDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        strategyDescriptor.ImplementationType.ShouldBe(typeof(KyrolusParallelNotificationPublishStrategy));
    }
    [Fact(DisplayName = "AddKyrolusMediator registers the sequential notification publish strategy when configured")]
    public void AddKyrolusMediator_registers_sequential_notification_strategy_when_configured()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(configuration =>
        {
            configuration.NotificationPublishMode = NotificationPublishMode.Sequential;
        });

        var strategyDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusNotificationPublishStrategy));
        strategyDescriptor.ShouldNotBeNull();
        strategyDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        strategyDescriptor.ImplementationType.ShouldBe(typeof(KyrolusSequentialNotificationPublishStrategy));
    }
    [Fact(DisplayName = "AddKyrolusMediator registers built-in pipeline behaviors")]
    public void AddKyrolusMediator_registers_built_in_pipeline_behaviors()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(configuration => { });

        var behaviorDescriptors = services.Where(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<,>)).ToList();
        behaviorDescriptors.ShouldNotBeEmpty();

        behaviorDescriptors.ShouldContain(d => d.ImplementationType == typeof(KyrolusRequestExceptionProcessorBehavior<,>));
        behaviorDescriptors.ShouldContain(d => d.ImplementationType == typeof(KyrolusRequestPreProcessorBehavior<,>));
        behaviorDescriptors.ShouldContain(d => d.ImplementationType == typeof(KyrolusRequestPostProcessorBehavior<,>));

        var streamBehaviorDescriptors = services.Where(d => d.ServiceType == typeof(IKyrolusStreamPipelineBehavior<,>)).ToList();
        streamBehaviorDescriptors.ShouldContain(d => d.ImplementationType == typeof(KyrolusStreamPassThroughBehavior<,>));
    }

    [Fact(DisplayName = "AddKyrolusMediator parameterless overload registers default mediator services")]
    public void AddKyrolusMediator_parameterless_overload_registers_default_services()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();

        var mediatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKyrolusMediator));
        mediatorDescriptor.ShouldNotBeNull();
        mediatorDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact(DisplayName = "AddKyrolusMediator registers configured open, closed, open stream, and closed stream behaviors")]
    public void AddKyrolusMediator_registers_configured_behaviors()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator(config =>
        {
            config.AddBehavior(typeof(ClosedBehaviorWithNonGenericInterface));
            config.AddOpenBehavior(typeof(OpenPipelineBehavior<,>));
            config.AddBehavior(typeof(ClosedStreamBehavior));
            config.AddOpenBehavior(typeof(OpenStreamBehavior<,>));
        });

        services.ShouldContain(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<Ping, string>) && d.ImplementationType == typeof(ClosedBehaviorWithNonGenericInterface));
        services.ShouldContain(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<,>) && d.ImplementationType == typeof(OpenPipelineBehavior<,>));
        services.ShouldContain(d => d.ServiceType == typeof(IKyrolusStreamPipelineBehavior<CountTo, int>) && d.ImplementationType == typeof(ClosedStreamBehavior));
        services.ShouldContain(d => d.ServiceType == typeof(IKyrolusStreamPipelineBehavior<,>) && d.ImplementationType == typeof(OpenStreamBehavior<,>));
    }

    [Fact(DisplayName = "AddBuiltInBehaviors checks IsDynamicCodeSupported and registers behaviors in dynamic environments")]
    public void AddBuiltInBehaviors_checks_IsDynamicCodeSupported_and_registers_behaviors()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();

        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            services.ShouldContain(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<,>) && d.ImplementationType == typeof(KyrolusRequestPreProcessorBehavior<,>));
            services.ShouldContain(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<,>) && d.ImplementationType == typeof(KyrolusRequestPostProcessorBehavior<,>));
            services.ShouldContain(d => d.ServiceType == typeof(IKyrolusPipelineBehavior<,>) && d.ImplementationType == typeof(KyrolusRequestExceptionProcessorBehavior<,>));
            services.ShouldContain(d => d.ServiceType == typeof(IKyrolusStreamPipelineBehavior<,>) && d.ImplementationType == typeof(KyrolusStreamPassThroughBehavior<,>));
        }
    }
    #endregion
}
