using System.IO;
using System.Text;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class ExceptionHandlingMiddlewareTests
{
    private static KyrolusExceptionHandlingDependencies CreateDependencies(
        TestLogger<KyrolusExceptionHandlingDependencies> logger,
        Action<KyrolusExceptionHandlingOptions>? configureOptions = null)
    {
        var options = new KyrolusExceptionHandlingOptions();
        configureOptions?.Invoke(options);
        var optionsWrapper = Options.Create(options);

        var contextFactory = new KyrolusHttpErrorContextFactory(optionsWrapper);
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper(),
            new KyrolusDefaultExceptionMapper()
        };
        var mappingService = new KyrolusExceptionMappingService(mappers, new KyrolusNullErrorLocalizer());
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(optionsWrapper);
        var environment = new TestHostEnvironment("Development");
        var translator = new KyrolusExceptionTranslator(mappingService, sanitizer, environment, optionsWrapper);
        var writer = new KyrolusJsonErrorResponseWriter();

        return new KyrolusExceptionHandlingDependencies(
            translator,
            writer,
            contextFactory,
            optionsWrapper,
            logger);
    }

    [Fact(DisplayName = "Middleware should pass request through next when no exception is thrown")]
    public async Task Invoke_Should_PassThrough_When_No_Exception()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var deps = CreateDependencies(logger);

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();

        await middleware.Invoke(context);

        nextCalled.ShouldBeTrue();
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Middleware should catch unhandled exception, write response and log error")]
    public async Task Invoke_Should_Catch_Exception_Write_Response_And_Log()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var deps = CreateDependencies(logger);

        RequestDelegate next = _ => throw new InvalidOperationException("Something went wrong");

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.ShouldBe("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("\"code\":\"internal_error\"");
        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Error);
        logger.Logs[0].Message.ShouldContain("internal_error");
    }

    [Fact(DisplayName = "Middleware should not log when exception mapping has ShouldLog set to false")]
    public async Task Invoke_Should_Not_Log_When_ShouldLog_Is_False()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var deps = CreateDependencies(logger);

        RequestDelegate next = _ => throw new KyrolusNotFoundException("User", "123");

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Middleware should not log when LogUnhandledExceptions is disabled in options")]
    public async Task Invoke_Should_Not_Log_When_LogUnhandledExceptions_Is_Disabled()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var deps = CreateDependencies(logger, opts => opts.LogUnhandledExceptions = false);

        RequestDelegate next = _ => throw new InvalidOperationException("Fatal error");

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Middleware should not log when exception type is in IgnoredExceptionLogTypes")]
    public async Task Invoke_Should_Not_Log_When_Exception_Is_Ignored_Type()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var deps = CreateDependencies(logger, opts => opts.IgnoredExceptionLogTypes.Add(typeof(OperationCanceledException)));

        RequestDelegate next = _ => throw new OperationCanceledException("Operation was cancelled");

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Middleware should use custom LogLevelSelector from options")]
    public async Task Invoke_Should_Use_Custom_LogLevelSelector()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var deps = CreateDependencies(logger, opts =>
        {
            opts.LogLevelSelector = (_, _) => LogLevel.Warning;
        });

        RequestDelegate next = _ => throw new InvalidOperationException("Custom level error");

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Warning);
    }

    [Fact(DisplayName = "Middleware should not write log when logger.IsEnabled returns false")]
    public async Task Invoke_Should_Not_Log_When_Logger_IsNotEnabled()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies> { Enabled = false };
        var deps = CreateDependencies(logger);

        RequestDelegate next = _ => throw new InvalidOperationException("Something went wrong");

        var middleware = new ExceptionHandlingMiddleware(next, deps);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);
        logger.Logs.ShouldBeEmpty();
    }
}
