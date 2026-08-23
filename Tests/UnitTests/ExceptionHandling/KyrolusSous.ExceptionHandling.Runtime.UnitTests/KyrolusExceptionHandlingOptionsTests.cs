namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class KyrolusExceptionHandlingOptionsTests
{
    [Fact(DisplayName = "IgnoreCommonNoisyExceptions should add common cancellation and HTTP exceptions")]
    public void IgnoreCommonNoisyExceptions_Should_Add_Expected_Types()
    {
        var options = new KyrolusExceptionHandlingOptions();

        options.IgnoreCommonNoisyExceptions();

        options.IgnoredExceptionLogTypes.ShouldContain(typeof(OperationCanceledException));
        options.IgnoredExceptionLogTypes.ShouldContain(typeof(TaskCanceledException));
        options.IgnoredExceptionLogTypes.ShouldContain(typeof(BadHttpRequestException));
    }

    [Fact(DisplayName = "IgnoreLoggingFor generic and non-generic should register types")]
    public void IgnoreLoggingFor_Should_Register_Exception_Types()
    {
        var options = new KyrolusExceptionHandlingOptions();

        options.IgnoreLoggingFor<TimeoutException>();
        options.IgnoreLoggingFor(typeof(HttpRequestException));

        options.IgnoredExceptionLogTypes.ShouldContain(typeof(TimeoutException));
        options.IgnoredExceptionLogTypes.ShouldContain(typeof(HttpRequestException));
    }

    [Fact(DisplayName = "IgnoreLoggingFor with null type should throw ArgumentNullException")]
    public void IgnoreLoggingFor_Should_Throw_On_Null()
    {
        var options = new KyrolusExceptionHandlingOptions();

        Should.Throw<ArgumentNullException>(() => options.IgnoreLoggingFor(null!));
    }

    [Fact(DisplayName = "LogLevelSelector default implementation should return Error for >= 500 and Warning for < 500")]
    public void LogLevelSelector_Default_Should_Select_Appropriate_Level()
    {
        var options = new KyrolusExceptionHandlingOptions();

        var serverErrorMapping = KyrolusExceptionMapping.Create(
            code: "internal_error",
            title: "Internal Error",
            statusCode: HttpStatusCode.InternalServerError);

        var clientErrorMapping = KyrolusExceptionMapping.Create(
            code: "bad_request",
            title: "Bad Request",
            statusCode: HttpStatusCode.BadRequest);

        var level500 = options.LogLevelSelector(serverErrorMapping, new Exception());
        var level400 = options.LogLevelSelector(clientErrorMapping, new Exception());

        level500.ShouldBe(LogLevel.Error);
        level400.ShouldBe(LogLevel.Warning);
    }
}
