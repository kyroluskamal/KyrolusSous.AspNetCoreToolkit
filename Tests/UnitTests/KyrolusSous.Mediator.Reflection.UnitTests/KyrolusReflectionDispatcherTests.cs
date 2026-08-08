

namespace KyrolusSous.Mediator.Reflection.UnitTests;

public class KyrolusReflectionDispatcherTests
{
    #region  DispatchRequestAsync
    [Fact(DisplayName = "DispatchRequestAsync throws exception when request is null")]
    public void DispatchRequestAsync_ThrowsException_WhenRequestIsNull()
    {
        KyrolusReflectionDispatcherTestsHelper.TestIf_ThrowIfQueryIsNull();
    }
    [Fact(DisplayName = "DispatchRequestAsync throws exception when serviceProvider is null")]
    public void DispatchRequestAsync_ThrowsException_WhenServiceProviderIsNull()
    {
        KyrolusReflectionDispatcherTestsHelper.TestIf_ThrowIfServiceProviderIsNullForQuery();
    }
    [Fact(DisplayName = "DispatchRequestAsync throws exception when no handler is registered")]
    public void DispatchRequestAsync_ThrowsException_WhenNoHandlerIsRegistered()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var request = new TestQuery();

        var dispatcher = new KyrolusReflectionDispatcher();
        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());
        exception.Message.ShouldBe($"[KyrolusMediator] No handler registered for {request.GetType().FullName} returning {typeof(string).FullName}.");
    }

    [Fact(DisplayName = "DispatchRequestAsync should throw exception when the Query is registerd with IKyrolusRequetHander")]
    public void DispatchRequestAsync_ShouldThrowException_WhenQueryIsRegisteredWithIKyrolusRequestHandler()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<TestQuery, string>, TestQueryHandlerWithRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestQuery();
        var dispatcher = new KyrolusReflectionDispatcher();

        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());
        exception.Message.ShouldBe($"[KyrolusMediator] No handler registered for {request.GetType().FullName} returning {typeof(string).FullName}.");
    }

    [Fact(DisplayName = "DispatchRequestAsync should invoke handler when handler is registered")]
    public async Task DispatchRequestAsync_ShouldInvokeHandler_WhenHandlerIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusQueryHandler<TestQuery, string>, TestQueryHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestQuery();
        var dispatcher = new KyrolusReflectionDispatcher();

        var result = await dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None);

        result.ShouldBe(request.Value);
    }

    [Fact(DisplayName = "DispatchRequestAsync should invoke handler when handler is registered with IKyrolusRequestHandler to handler IKeyrolusRequest")]
    public async Task DispatchRequestAsync_ShouldInvokeHandler_WhenHandlerIsRegisteredWithIKyrolusRequestHandler()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<TestRequest, string>, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestRequest();
        var dispatcher = new KyrolusReflectionDispatcher();

        var result = await dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None);

        result.ShouldBe(request.Value);
    }

    [Fact(DisplayName = "DispatchRequestAsync should throw exception when handler is registered with IKyrolusCommandHandler for Command that does not return a response")]
    public async Task DispatchRequestAsync_ShouldThrowException_WhenHandlerIsRegisteredWithIKyrolusCommandHandlerForCommandThatDoesNotReturnResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusCommandHandler<TestCommand>, TestCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommand();
        var dispatcher = new KyrolusReflectionDispatcher();

        var exception = Should.Throw<ArgumentException>(() =>
        dispatcher.DispatchRequestAsync<string>(command, serviceProvider, CancellationToken.None));
    }

    [Fact(DisplayName = "DispatchRequestAsync should invoke handler when handler is registered with IKyrolusCommandHandler to handler IKeyrolusCommandWithResponse")]
    public async Task DispatchRequestAsync_ShouldInvokeHandler_WhenHandlerIsRegisteredWithIKyrolusCommandHandlerAndIKeyrolusCommandWithResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusCommandHandler<TestCommandWithRespone, string>, TestCommandWithResponeHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommandWithRespone();
        var dispatcher = new KyrolusReflectionDispatcher();

        var result = await dispatcher.DispatchRequestAsync<string>(command, serviceProvider, CancellationToken.None);

        result.ShouldBe(command.Value);
    }
    #endregion

    #region DispatchCommandAsync
    [Fact(DisplayName = "DispatchCommandAsync throws exception when command is null")]
    public void DispatchCommandAsync_ThrowsException_WhenCommandIsNull()
    {
        KyrolusReflectionDispatcherTestsHelper.TestIf_ThrowIfCommandIsNull();
    }
    [Fact(DisplayName = "DispatchCommandAsync throws exception when serviceProvider is null")]
    public void DispatchCommandAsync_ThrowsException_WhenServiceProviderIsNull()
    {
        KyrolusReflectionDispatcherTestsHelper.TestIf_ThrowIfServiceProviderIsNullForCommand();
    }
    [Fact(DisplayName = "DispatchCommandAsync throws exception when no handler is registered")]
    public void DispatchCommandAsync_ThrowsException_WhenNoHandlerIsRegistered()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var command = new TestCommand();

        var dispatcher = new KyrolusReflectionDispatcher();
        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchCommandAsync(command, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());
        exception.Message.ShouldBe($"[KyrolusMediator] No handler registered for command {command.GetType().FullName}.");
    }
    [Fact(DisplayName = "DispatchCommandAsync should invoke handler when handler is registered with IKyrolusCommandHandler to handle IKyrolusCommand without response")]
    public async Task DispatchCommandAsync_ShouldInvokeHandler_WhenHandlerIsRegisteredWithIKyrolusCommandHandlerToHandleIKyrolusCommandWithoutResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusCommandHandler<TestCommand>, TestCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommand();
        var dispatcher = new KyrolusReflectionDispatcher();

        await dispatcher.DispatchCommandAsync(command, serviceProvider, CancellationToken.None);
    }
    [Fact(DisplayName = "DispatchCommandAsync should throw exception when handler is registered with IKyrolusCommandHandler to handle IKyrolusCommand with response")]
    public void DispatchCommandAsync_ShouldThrowException_WhenHandlerIsRegisteredWithIKyrolusCommandHandlerToHandleIKyrolusCommandWithResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusCommandHandler<TestCommandWithRespone, string>, TestCommandWithResponeHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommandWithRespone();
        var dispatcher = new KyrolusReflectionDispatcher();

        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchCommandAsync(command, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());
        exception.Message.ShouldBe($"[KyrolusMediator] No handler registered for command {command.GetType().FullName}.");
    }
    [Fact(DisplayName = "DispatchCommandAsync should invoke handler when handler is registered with IKyrolusRequestHandler to handle IKyrolusRequest without response")]
    public async Task DispatchCommandAsync_ShouldInvokeHandler_WhenHandlerIsRegisteredWithIKyrolusRequestHandlerToHandleIKyrolusRequestWithoutResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<TestCommand_Without_Respone_And_IKyrolusRequest>, TestCommand_Without_Respone_And_IKyrolusRequest_Handler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommand_Without_Respone_And_IKyrolusRequest();
        var dispatcher = new KyrolusReflectionDispatcher();

        await dispatcher.DispatchCommandAsync(command, serviceProvider, CancellationToken.None);
    }

    [Fact(DisplayName = "DispatchCommandAsync should throw exception when handler is registered with IKyrolusRequestHandler to handle IKyrolusRequest with response")]
    public void DispatchCommandAsync_ShouldThrowException_WhenHandlerIsRegisteredWithIKyrolusRequestHandlerToHandleIKyrolusRequestWithResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<TestCommand_With_Respone_And_IKyrolusRequest, string>, TestCommand_With_Respone_And_IKyrolusRequest_Handler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommand_With_Respone_And_IKyrolusRequest();
        var dispatcher = new KyrolusReflectionDispatcher();

        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchCommandAsync(command, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());
        exception.Message.ShouldBe($"[KyrolusMediator] No handler registered for command {command.GetType().FullName}.");
    }

    [Fact(DisplayName = "DispatchCommandAsync should invoke handler when handler is registered with IKyrolusRequestHandler to handle IKyrolusRequest<Unit> without response")]
    public async Task DispatchCommandAsync_ShouldInvokeHandler_WhenHandlerIsRegisteredWithIKyrolusRequestHandlerToHandleIKyrolusRequestUnitWithoutResponse()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<TestCommand_Without_Respone_And_IKyrolusRequest_unit>, TestCommand_Without_Respone_And_IKyrolusRequest_unit_Handler>();
        var serviceProvider = services.BuildServiceProvider();

        var command = new TestCommand_Without_Respone_And_IKyrolusRequest_unit();
        var dispatcher = new KyrolusReflectionDispatcher();

        await dispatcher.DispatchCommandAsync(command, serviceProvider, CancellationToken.None);
    }
    #endregion

    #region DispatchStreamAsync
    [Fact(DisplayName = "DispatchStreamAsync throws exception when stream is null")]
    public void DispatchStreamAsync_ThrowsException_WhenStreamIsNull()
    {
        KyrolusReflectionDispatcherTestsHelper.TestIf_ThrowIfStreamIsNull();
    }
    [Fact(DisplayName = "DispatchStreamAsync throws exception when serviceProvider is null")]
    public void DispatchStreamAsync_ThrowsException_WhenServiceProviderIsNull()
    {
        KyrolusReflectionDispatcherTestsHelper.TestIf_ThrowIfServiceProviderIsNullForStream();
    }
    [Fact(DisplayName = "DispatchStreamAsync throws exception when no handler is registered")]
    public void DispatchStreamAsync_ThrowsException_WhenNoHandlerIsRegistered()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var request = new TestStream();

        var dispatcher = new KyrolusReflectionDispatcher();
        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchStreamAsync<string>(request, serviceProvider, CancellationToken.None).GetAsyncEnumerator().MoveNextAsync().AsTask().GetAwaiter().GetResult());
        exception.Message.ShouldBe($"[KyrolusMediator] No stream handler registered for {request.GetType().FullName} producing {typeof(string).FullName}.");
    }

    [Fact(DisplayName = "DispatchStreamAsync should invoke handler when handler is registered")]
    public async Task DispatchStreamAsync_ShouldInvokeHandler_WhenHandlerIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusStreamRequestHandler<TestStream, string>, TestStreamHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestStream();
        var dispatcher = new KyrolusReflectionDispatcher();

        var stream = dispatcher.DispatchStreamAsync<string>(request, serviceProvider, CancellationToken.None);
        var items = new List<string>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        items.ShouldContain("This is a test stream");
        items.ShouldContain("This is the second item in the stream");
    }
    #endregion

    #region Reflection Failure and Unwrapping Tests
    [Fact(DisplayName = "DispatchRequestAsync throws InvalidOperationException when Handle method is not found on handler type")]
    public void DispatchRequestAsync_ThrowsInvalidOperationException_WhenHandleMethodNotFoundOnHandler()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<ExplicitHandleRequest, string>, ExplicitHandleRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new ExplicitHandleRequest();
        var dispatcher = new KyrolusReflectionDispatcher();

        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());

        exception.Message.ShouldContain("Could not find Handle");
    }

    [Fact(DisplayName = "DispatchRequestAsync unwraps TargetInvocationException and rethrows original handler exception")]
    public void DispatchRequestAsync_UnwrapsTargetInvocationException_AndRethrowsInnerException()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<ThrowingRequest, string>, ThrowingRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new ThrowingRequest();
        var dispatcher = new KyrolusReflectionDispatcher();

        var exception = Should.Throw<InvalidOperationException>(() =>
            dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None).GetAwaiter().GetResult());

        exception.Message.ShouldBe("Custom Handler Error");
    }
    #endregion

    #region Cache Tests
    [Fact(DisplayName = "DispatchRequestAsync caches MethodInfo in handle method cache on subsequent dispatches")]
    public async Task DispatchRequestAsync_CachesMethodInfo_OnSubsequentDispatches()
    {
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusRequestHandler<TestRequest, string>, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestRequest();
        var dispatcher = new KyrolusReflectionDispatcher();

        // First dispatch
        await dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None);

        // Access internal s_handleMethodCache field via reflection to inspect cache state
        var field = typeof(KyrolusReflectionDispatcher).GetField("s_handleMethodCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (ConcurrentDictionary<(Type HandlerType, Type RequestType), MethodInfo>)field.GetValue(null)!;

        var initialCount = cache.Count;
        initialCount.ShouldBeGreaterThan(0);

        // Second dispatch with same request type
        await dispatcher.DispatchRequestAsync<string>(request, serviceProvider, CancellationToken.None);

        // Cache count should remain identical (reused)
        cache.Count.ShouldBe(initialCount);
    }
    #endregion
}
