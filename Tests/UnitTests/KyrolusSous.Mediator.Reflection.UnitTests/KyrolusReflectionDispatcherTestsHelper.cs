namespace KyrolusSous.Mediator.Reflection.UnitTests;

public class KyrolusReflectionDispatcherTestsHelper
{
    private static ArgumentNullException TestIf_ThrowIfQueryOrCommandOrStreamIsNull(bool isQuery = true, bool isStream = false)
    {
        // Arrange
        var dispatcher = new KyrolusReflectionDispatcher();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var cancellationToken = CancellationToken.None;

        // Act & Assert using Shoudly
        var exception = Should.Throw<ArgumentNullException>(() =>
           isQuery ? dispatcher.DispatchRequestAsync<object>(null!, serviceProvider, cancellationToken) :
           isStream ? dispatcher.DispatchStreamAsync<object>(null!, serviceProvider, cancellationToken) :
           dispatcher.DispatchCommandAsync(null!, serviceProvider, cancellationToken));
        exception.ParamName.ShouldBe(isQuery || isStream ? "request" : "command");
        return exception;
    }

    public static ArgumentNullException TestIf_ThrowIfQueryIsNull()
    => TestIf_ThrowIfQueryOrCommandOrStreamIsNull(isQuery: true, isStream: false);
    public static ArgumentNullException TestIf_ThrowIfCommandIsNull()
    => TestIf_ThrowIfQueryOrCommandOrStreamIsNull(isQuery: false, isStream: false);

    public static ArgumentNullException TestIf_ThrowIfStreamIsNull()
    => TestIf_ThrowIfQueryOrCommandOrStreamIsNull(isQuery: false, isStream: true);



    private static void TestIf_ThrowIfServiceProviderIsNull(bool isQuery = true, bool isStream = false)
    {
        // Arrange
        var dispatcher = new KyrolusReflectionDispatcher();
        var requestOrCommandOrStream = new object();
        var cancellationToken = CancellationToken.None;

        // Act & Assert using Shoudly
        var exception = Should.Throw<ArgumentNullException>(() =>
           isQuery ? dispatcher.DispatchRequestAsync<object>(requestOrCommandOrStream, null!, cancellationToken) :
           isStream ? dispatcher.DispatchStreamAsync<object>(requestOrCommandOrStream, null!, cancellationToken) :
           dispatcher.DispatchCommandAsync(requestOrCommandOrStream, null!, cancellationToken));
        exception.ParamName.ShouldBe("serviceProvider");
    }

    public static void TestIf_ThrowIfServiceProviderIsNullForQuery()
    {
        TestIf_ThrowIfServiceProviderIsNull(isQuery: true, isStream: false);
    }

    public static void TestIf_ThrowIfServiceProviderIsNullForCommand()
    {
        TestIf_ThrowIfServiceProviderIsNull(isQuery: false, isStream: false);
    }

    public static void TestIf_ThrowIfServiceProviderIsNullForStream()
    {
        TestIf_ThrowIfServiceProviderIsNull(isQuery: false, isStream: true);
    }
}
