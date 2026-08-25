namespace KyrolusSous.Mediator.Reflection.UnitTests;

public class ReflectionSourcesTests
{
    #region ReflectionPipelineWrapperSource Tests
    [Fact(DisplayName = "CreateRequestWrapper should return valid pipeline wrapper object")]
    public void CreateRequestWrapper_ShouldReturnValidWrapper()
    {
        // Arrange
        var source = new ReflectionPipelineWrapperSource();

        // Act
        var wrapper = source.CreateRequestWrapper(typeof(TestRequest), typeof(string));

        // Assert
        wrapper.ShouldNotBeNull();
        wrapper.GetType().Name.ShouldStartWith("RequestPipelineWrapperImpl");
    }

    [Fact(DisplayName = "CreateStreamWrapper should return valid stream pipeline wrapper object")]
    public void CreateStreamWrapper_ShouldReturnValidStreamWrapper()
    {
        // Arrange
        var source = new ReflectionPipelineWrapperSource();

        // Act
        var wrapper = source.CreateStreamWrapper(typeof(TestStream), typeof(string));

        // Assert
        wrapper.ShouldNotBeNull();
        wrapper.GetType().Name.ShouldStartWith("StreamPipelineWrapperImpl");
    }

    [Fact(DisplayName = "GetResponseType should return response type for request implementing single interface")]
    public void GetResponseType_ShouldReturnResponseType_ForSingleInterfaceRequest()
    {
        // Arrange
        var source = new ReflectionPipelineWrapperSource();

        // Act
        var requestResponseType = source.GetResponseType(typeof(TestRequest), stream: false);
        var streamResponseType = source.GetResponseType(typeof(TestStream), stream: true);

        // Assert
        requestResponseType.ShouldBe(typeof(string));
        streamResponseType.ShouldBe(typeof(string));
    }

    [Fact(DisplayName = "GetResponseType should return null when request implements zero or multiple response interfaces")]
    public void GetResponseType_ShouldReturnNull_ForZeroOrMultipleResponseInterfaces()
    {
        // Arrange
        var source = new ReflectionPipelineWrapperSource();

        // Act
        var zeroInterfaceType = source.GetResponseType(typeof(NoResponseRequest), stream: false);
        var multipleInterfaceType = source.GetResponseType(typeof(MultipleResponseRequest), stream: false);

        // Assert
        zeroInterfaceType.ShouldBeNull();
        multipleInterfaceType.ShouldBeNull();
    }
    #endregion

    #region ReflectionNotificationDispatchSource Tests
    [Fact(DisplayName = "CreateHandlerInvocations should invoke registered notification handlers successfully")]
    public async Task CreateHandlerInvocations_ShouldInvokeRegisteredHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusNotificationHandler<TestNotification>, TestNotificationHandler1>();
        services.AddTransient<IKyrolusNotificationHandler<TestNotification>, TestNotificationHandler2>();
        var serviceProvider = services.BuildServiceProvider();

        var notification = new TestNotification("Hello World");
        var source = new ReflectionNotificationDispatchSource();

        // Act
        var invocations = source.CreateHandlerInvocations(notification, serviceProvider);

        // Assert
        invocations.Count.ShouldBe(2);
        foreach (var invocation in invocations)
        {
            await invocation(CancellationToken.None);
        }
    }

    [Fact(DisplayName = "CreateHandlerInvocations should unwrap TargetInvocationException when handler throws")]
    public async Task CreateHandlerInvocations_ShouldUnwrapTargetInvocationException_WhenHandlerThrows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusNotificationHandler<TestNotification>, ThrowingNotificationHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var notification = new TestNotification("Test Throw");
        var source = new ReflectionNotificationDispatchSource();

        // Act
        var invocations = source.CreateHandlerInvocations(notification, serviceProvider);

        // Assert
        invocations.Count.ShouldBe(1);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await invocations[0](CancellationToken.None);
        });

        exception.Message.ShouldBe("Notification Handler Error");
    }

    [Fact(DisplayName = "CreateHandlerInvocations should throw InvalidOperationException when Handle method is not found")]
    public async Task CreateHandlerInvocations_ShouldThrowInvalidOperationException_WhenHandleMethodNotFound()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IKyrolusNotificationHandler<TestNotification>, ExplicitNotificationHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var notification = new TestNotification("Explicit");
        var source = new ReflectionNotificationDispatchSource();

        // Act
        var invocations = source.CreateHandlerInvocations(notification, serviceProvider);

        // Assert
        invocations.Count.ShouldBe(1);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await invocations[0](CancellationToken.None);
        });

        exception.Message.ShouldContain("Could not find Handle");
    }
    #endregion

    #region ReflectionRequestExceptionDispatchSource Tests
    [Fact(DisplayName = "CreateActionInvocations should invoke registered exception actions successfully")]
    public async Task CreateActionInvocations_ShouldInvokeRegisteredExceptionActions()
    {
        // Arrange
        var action = new TestRequestExceptionAction();
        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusRequestExceptionAction<TestRequest, InvalidOperationException>>(action);
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestRequest();
        var exception = new InvalidOperationException("Test Exception");
        var source = new ReflectionRequestExceptionDispatchSource();

        // Act
        var invocations = source.CreateActionInvocations(typeof(TestRequest), typeof(InvalidOperationException), request, exception, serviceProvider);

        // Assert
        invocations.Count.ShouldBe(1);
        invocations[0].ActionType.ShouldBe(typeof(TestRequestExceptionAction));

        await invocations[0].Invoke(CancellationToken.None);
        action.Executed.ShouldBeTrue();
    }

    [Fact(DisplayName = "CreateHandlerInvocations should invoke registered exception handlers successfully")]
    public async Task CreateHandlerInvocations_ShouldInvokeRegisteredExceptionHandlers()
    {
        // Arrange
        var exceptionHandler = new TestRequestExceptionHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusRequestExceptionHandler<TestRequest, InvalidOperationException, string>>(exceptionHandler);
        var serviceProvider = services.BuildServiceProvider();

        var request = new TestRequest();
        var exception = new InvalidOperationException("Test Exception");
        var state = new KyrolusRequestExceptionHandlerState<string>();
        var source = new ReflectionRequestExceptionDispatchSource();

        // Act
        var invocations = source.CreateHandlerInvocations(
            typeof(TestRequest),
            typeof(InvalidOperationException),
            typeof(string),
            request,
            exception,
            state,
            serviceProvider);

        // Assert
        invocations.Count.ShouldBe(1);

        await invocations[0](CancellationToken.None);
        exceptionHandler.Handled.ShouldBeTrue();
        state.Handled.ShouldBeTrue();
        state.Response.ShouldBe("HandledByTestException");
    }
    #endregion
}
