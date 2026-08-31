using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class KyrolusExceptionFilterTests
{
    private static KyrolusExceptionFilter CreateFilter(
        TestLogger<KyrolusExceptionFilter> logger,
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
        var mappingService = new KyrolusExceptionMappingService(mappers);
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(optionsWrapper);
        var environment = new TestHostEnvironment("Development");
        var translator = new KyrolusExceptionTranslator(mappingService, sanitizer, environment, optionsWrapper);
        var writer = new KyrolusJsonErrorResponseWriter();

        return new KyrolusExceptionFilter(
            translator,
            writer,
            contextFactory,
            optionsWrapper,
            logger);
    }

    private static ExceptionContext CreateExceptionContext(Exception exception, bool exceptionHandled = false)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception,
            ExceptionHandled = exceptionHandled
        };
    }

    [Fact(DisplayName = "Filter should return immediately when ExceptionHandled is already true")]
    public async Task OnExceptionAsync_Should_Return_When_Already_Handled()
    {
        var logger = new TestLogger<KyrolusExceptionFilter>();
        var filter = CreateFilter(logger);

        var context = CreateExceptionContext(new InvalidOperationException("Already handled"), exceptionHandled: true);

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        context.Result.ShouldBeNull();
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Filter should handle exception, set result, write response and log error")]
    public async Task OnExceptionAsync_Should_Handle_Exception_Write_Response_And_Log()
    {
        var logger = new TestLogger<KyrolusExceptionFilter>();
        var filter = CreateFilter(logger);

        var context = CreateExceptionContext(new InvalidOperationException("Database crashed"));

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        context.Result.ShouldBeOfType<EmptyResult>();
        context.HttpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);
        context.HttpContext.Response.ContentType.ShouldBe("application/json");

        context.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.HttpContext.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("\"code\":\"internal_error\"");
        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Error);
        logger.Logs[0].Message.ShouldContain("internal_error");
    }

    [Fact(DisplayName = "Filter should not log when exception mapping has ShouldLog set to false")]
    public async Task OnExceptionAsync_Should_Not_Log_When_ShouldLog_Is_False()
    {
        var logger = new TestLogger<KyrolusExceptionFilter>();
        var filter = CreateFilter(logger);

        var context = CreateExceptionContext(new KyrolusNotFoundException("Order", "999"));

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        context.HttpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Filter should not log when LogUnhandledExceptions is disabled in options")]
    public async Task OnExceptionAsync_Should_Not_Log_When_LogUnhandledExceptions_Is_Disabled()
    {
        var logger = new TestLogger<KyrolusExceptionFilter>();
        var filter = CreateFilter(logger, opts => opts.LogUnhandledExceptions = false);

        var context = CreateExceptionContext(new InvalidOperationException("Fatal error"));

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Filter should not log when exception type is in IgnoredExceptionLogTypes")]
    public async Task OnExceptionAsync_Should_Not_Log_When_Exception_Is_Ignored_Type()
    {
        var logger = new TestLogger<KyrolusExceptionFilter>();
        var filter = CreateFilter(logger, opts => opts.IgnoredExceptionLogTypes.Add(typeof(OperationCanceledException)));

        var context = CreateExceptionContext(new OperationCanceledException("Operation was cancelled"));

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Filter should not log when logger.IsEnabled returns false")]
    public async Task OnExceptionAsync_Should_Not_Log_When_Logger_IsNotEnabled()
    {
        var logger = new TestLogger<KyrolusExceptionFilter> { Enabled = false };
        var filter = CreateFilter(logger);

        var context = CreateExceptionContext(new InvalidOperationException("Some error"));

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        logger.Logs.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Filter should use custom LogLevelSelector from options")]
    public async Task OnExceptionAsync_Should_Use_Custom_LogLevelSelector()
    {
        var logger = new TestLogger<KyrolusExceptionFilter>();
        var filter = CreateFilter(logger, opts =>
        {
            opts.LogLevelSelector = (_, _) => LogLevel.Warning;
        });

        var context = CreateExceptionContext(new InvalidOperationException("Custom level error"));

        await filter.OnExceptionAsync(context);

        context.ExceptionHandled.ShouldBeTrue();
        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Warning);
    }
}
