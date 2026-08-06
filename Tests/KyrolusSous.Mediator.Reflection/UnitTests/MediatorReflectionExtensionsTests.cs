using KyrolusSous.Mediator.Runtime.GeneratorIntegration;

namespace KyrolusSous.Mediator.Reflection.UnitTests;

public class MediatorReflectionExtensionsTests
{
    #region AddKyrolusMediatorReflection
    [Fact(DisplayName = "AddKyrolusMediatorReflection should throw ArgumentNullException when serviceCollection is null")]
    public void AddKyrolusMediatorReflection_ShouldThrowArgumentNullException_WhenServiceCollectionIsNull()
    {
        // Arrange
        IServiceCollection? serviceCollection = null;

        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() => serviceCollection!.AddKyrolusMediatorReflection());
        exception.ParamName.ShouldBe("services");
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should throw InvalidOperationException when AddKyrolusMediator was not called first")]
    public void AddKyrolusMediatorReflection_ShouldThrowInvalidOperationException_WhenAddKyrolusMediatorWasNotCalledFirst()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => serviceCollection.AddKyrolusMediatorReflection());
        exception.Message.ShouldBe("[KyrolusMediator] AddKyrolusMediatorReflection() must be called after AddKyrolusMediator(), which is what records the assemblies to scan and the lifetimes to use.");
    }
    [Fact(DisplayName = "AddKyrolusMediatorReflection should register KyrolusReflectionDispatcher as IMediatorDispatcher")]
    public void AddKyrolusMediatorReflection_ShouldRegisterKyrolusReflectionDispatcher_AsIMediatorDispatcher()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddKyrolusMediator(configuration => { });
        serviceCollection.AddSingleton<IMediatorDispatcher, MediatorDispacherMock>();
        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var dispatcher = serviceProvider.GetService<IMediatorDispatcher>();

        // Assert
        dispatcher.ShouldNotBeNull();
        dispatcher.ShouldBeOfType<KyrolusReflectionDispatcher>();
        dispatcher.ShouldNotBeOfType<MediatorDispacherMock>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should register ReflectionPipelineWrapperSource as IKyrolusPipelineWrapperSource")]
    public void AddKyrolusMediatorReflection_ShouldRegisterReflectionPipelineWrapperSource_AsIKyrolusPipelineWrapperSource()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddKyrolusMediator(configuration => { });
        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var pipelineWrapperSource = serviceProvider.GetService<IKyrolusPipelineWrapperSource>();

        // Assert
        pipelineWrapperSource.ShouldNotBeNull();
        pipelineWrapperSource.ShouldBeOfType<ReflectionPipelineWrapperSource>();
    }
    [Fact(DisplayName = "AddKyrolusMediatorReflection should register ReflectionNotificationDispatchSource as IKyrolusNotificationDispatchSource")]
    public void AddKyrolusMediatorReflection_ShouldRegisterReflectionNotificationDispatchSource_AsIKyrolusNotificationDispatchSource()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddKyrolusMediator(configuration => { });
        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var notificationDispatchSource = serviceProvider.GetService<IKyrolusNotificationDispatchSource>();

        // Assert
        notificationDispatchSource.ShouldNotBeNull();
        notificationDispatchSource.ShouldBeOfType<ReflectionNotificationDispatchSource>();
    }
    [Fact(DisplayName = "AddKyrolusMediatorReflection should register ReflectionRequestExceptionDispatchSource as IKyrolusRequestExceptionDispatchSource")]
    public void AddKyrolusMediatorReflection_ShouldRegisterReflectionRequestExceptionDispatchSource_AsIKyrolusRequestExceptionDispatchSource()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddKyrolusMediator(configuration => { });
        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var requestExceptionDispatchSource = serviceProvider.GetService<IKyrolusRequestExceptionDispatchSource>();

        // Assert
        requestExceptionDispatchSource.ShouldNotBeNull();
        requestExceptionDispatchSource.ShouldBeOfType<ReflectionRequestExceptionDispatchSource>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should register handlers from assemblies")]
    public void AddKyrolusMediatorReflection_ShouldRegisterHandlers_FromAssemblies()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assembly = typeof(MediatorReflectionExtensionsTests).Assembly;
        serviceCollection.AddKyrolusMediator(configuration =>
        {
            configuration.ThrowOnDuplicateRequestHandlers = false;
            configuration.RegisterServicesFromAssemblies(assembly);
        });

        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var requestHandler = serviceProvider.GetService<IKyrolusRequestHandler<TestRequest, string>>();
        requestHandler.ShouldNotBeNull();
        requestHandler.ShouldBeOfType<TestRequestHandler>();

        var commandHandler = serviceProvider.GetService<IKyrolusCommandHandler<TestCommand>>();
        commandHandler.ShouldNotBeNull();
        commandHandler.ShouldBeOfType<TestCommandHandler>();

        var streamRequestHandler = serviceProvider.GetService<IKyrolusStreamRequestHandler<TestStream, string>>();
        streamRequestHandler.ShouldNotBeNull();
        streamRequestHandler.ShouldBeOfType<TestStreamHandler>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should throw InvalidOperationException when duplicate single handlers found and ThrowOnDuplicateRequestHandlers is true")]
    public void AddKyrolusMediatorReflection_ShouldThrowInvalidOperationException_WhenDuplicateSingleHandlersFound()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assembly = typeof(MediatorReflectionExtensionsTests).Assembly;
        serviceCollection.AddKyrolusMediator(configuration =>
        {
            configuration.ThrowOnDuplicateRequestHandlers = true; // Default
            configuration.RegisterServicesFromAssemblies(assembly);
        });

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => serviceCollection.AddKyrolusMediatorReflection());
        exception.Message.ShouldContain("Two handlers are registered for");
        exception.Message.ShouldContain(typeof(DuplicateTestQueryHandler1).FullName!);
        exception.Message.ShouldContain(typeof(DuplicateTestQueryHandler2).FullName!);
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should keep first handler when duplicate single handlers found and ThrowOnDuplicateRequestHandlers is false")]
    public void AddKyrolusMediatorReflection_ShouldKeepFirstHandler_WhenThrowOnDuplicateRequestHandlersIsFalse()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assembly = typeof(MediatorReflectionExtensionsTests).Assembly;
        serviceCollection.AddKyrolusMediator(configuration =>
        {
            configuration.ThrowOnDuplicateRequestHandlers = false;
            configuration.RegisterServicesFromAssemblies(assembly);
        });

        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var handler = serviceProvider.GetService<IKyrolusQueryHandler<DuplicateTestQuery, string>>();
        handler.ShouldNotBeNull();
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should register multiple notification handlers via TryAddEnumerable")]
    public void AddKyrolusMediatorReflection_ShouldRegisterMultipleNotificationHandlers()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assembly = typeof(MediatorReflectionExtensionsTests).Assembly;
        serviceCollection.AddKyrolusMediator(configuration =>
        {
            configuration.ThrowOnDuplicateRequestHandlers = false;
            configuration.RegisterServicesFromAssemblies(assembly);
        });

        // Act
        serviceCollection.AddKyrolusMediatorReflection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var notificationHandlers = serviceProvider.GetServices<INotificationHandler<TestNotification>>().ToList();
        notificationHandlers.Count.ShouldBeGreaterThanOrEqualTo(2);
        notificationHandlers.ShouldContain(h => h.GetType() == typeof(TestNotificationHandler1));
        notificationHandlers.ShouldContain(h => h.GetType() == typeof(TestNotificationHandler2));
    }
    #endregion

    #region  AddKyrolusMediatorFromAssemblies
    [Fact(DisplayName = "AddKyrolusMediatorFromAssemblies should throw ArgumentNullException when serviceCollection is null")]
    public void AddKyrolusMediatorFromAssemblies_ShouldThrowArgumentNullException_WhenServiceCollectionIsNull()
    {
        // Arrange
        IServiceCollection? serviceCollection = null;
        var assemblies = new[] { typeof(MediatorReflectionExtensionsTests).Assembly };

        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() => serviceCollection!.AddKyrolusMediatorFromAssemblies(assemblies));
        exception.ParamName.ShouldBe("services");
    }
    [Fact(DisplayName = "AddKyrolusMediatorFromAssemblies should throw ArgumentException when assemblies is null")]
    public void AddKyrolusMediatorFromAssemblies_ShouldThrowArgumentException_WhenAssembliesIsNull()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => serviceCollection.AddKyrolusMediatorFromAssemblies((Assembly[])null!));
        exception.ParamName.ShouldBe("assemblies");
    }   
    [Fact(DisplayName = "AddKyrolusMediatorFromAssemblies should throw ArgumentException when assemblies is empty")]
    public void AddKyrolusMediatorFromAssemblies_ShouldThrowArgumentException_WhenAssembliesIsEmpty()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => serviceCollection.AddKyrolusMediatorFromAssemblies([]));
        exception.ParamName.ShouldBe("assemblies");
    }
    [Fact(DisplayName = "AddKyrolusMediatorFromAssemblies should register handlers from assemblies")]
    public void AddKyrolusMediatorFromAssemblies_ShouldRegisterHandlersFromAssemblies()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assemblies = new[] { typeof(MediatorReflectionExtensionsTests).Assembly };

        // Act
        serviceCollection.AddKyrolusMediatorFromAssemblies(config => config.ThrowOnDuplicateRequestHandlers = false, typeof(MediatorReflectionExtensionsTests).Assembly);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var queryHandlers = serviceProvider.GetServices<IKyrolusQueryHandler<TestQuery, string>>().ToList();
        queryHandlers.Count.ShouldBe(1);
        serviceProvider.GetService<IKyrolusQueryHandler<TestQuery, string>>().ShouldBeOfType<TestQueryHandler>();
        var commandHandlers = serviceProvider.GetServices<IKyrolusCommandHandler<TestCommand>>().ToList();
        commandHandlers.Count.ShouldBe(1);
        serviceProvider.GetService<IKyrolusPipelineWrapperSource>().ShouldBeOfType<ReflectionPipelineWrapperSource>();
        serviceProvider.GetService<IKyrolusNotificationDispatchSource>().ShouldBeOfType<ReflectionNotificationDispatchSource>();
        serviceProvider.GetService<IKyrolusRequestExceptionDispatchSource>().ShouldBeOfType<ReflectionRequestExceptionDispatchSource>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorFromAssemblies default overload should register dispatcher and sources cleanly")]
    public void AddKyrolusMediatorFromAssemblies_DefaultOverload_ShouldRegisterDispatcherAndSourcesCleanly()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Act (Invokes default overload in line 76 without config Action)
        serviceCollection.AddKyrolusMediatorFromAssemblies(typeof(KyrolusReflectionDispatcher).Assembly);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IMediatorDispatcher>().ShouldBeOfType<KyrolusReflectionDispatcher>();
        serviceProvider.GetService<IKyrolusPipelineWrapperSource>().ShouldBeOfType<ReflectionPipelineWrapperSource>();
        serviceProvider.GetService<IKyrolusNotificationDispatchSource>().ShouldBeOfType<ReflectionNotificationDispatchSource>();
        serviceProvider.GetService<IKyrolusRequestExceptionDispatchSource>().ShouldBeOfType<ReflectionRequestExceptionDispatchSource>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorReflection should ignore abstract classes and interfaces during assembly scanning")]
    public void AddKyrolusMediatorReflection_ShouldIgnoreAbstractClassesAndInterfaces_DuringAssemblyScanning()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        var assemblies = new[] { typeof(MediatorReflectionExtensionsTests).Assembly };

        // Act
        serviceCollection.AddKyrolusMediatorFromAssemblies(c => c.ThrowOnDuplicateRequestHandlers = false, assemblies);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert
        var abstractHandlers = serviceProvider.GetServices<IKyrolusQueryHandler<TestQuery, string>>()
            .Where(h => h.GetType().IsAbstract)
            .ToList();

        abstractHandlers.ShouldBeEmpty();
    }
    #endregion
}
