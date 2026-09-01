namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

/// <summary>
/// Regression guard for the unification work: the middleware, the MVC exception filter, and every native
/// <see cref="IExceptionHandler"/> all delegate to the same <see cref="KyrolusExceptionHandlingDependencies"/>,
/// so the same exception must produce the identical status code and error code no matter which of the three
/// surfaces caught it. Before unification, the native handlers duplicated their own classification and could
/// (and did) silently drift from the middleware's - e.g. SocketException was 502 via one path and 500 via the
/// other.
/// </summary>
public class KyrolusExceptionHandlingConsistencyTests
{
    private static KyrolusExceptionHandlingDependencies CreateDependencies()
        // TraceId/CorrelationId fall back to each DefaultHttpContext's own auto-generated TraceIdentifier, which
        // differs per HttpContext instance regardless of the exception - turned off here so the JSON comparison
        // below reflects only the classification fields that unification is actually meant to keep identical.
        => TestExceptionHandlingDependenciesFactory.Create(
            environmentName: "Production",
            configureOptions: o =>
            {
                o.IncludeTraceId = false;
                o.IncludeCorrelationId = false;
            });

    public static TheoryData<Exception> SharedExceptionCases =>
        new()
        {
            new SocketException((int)SocketError.HostNotFound),
            new TimeoutException("timed out"),
            new JsonException("bad json"),
            new ArgumentException("bad arg"),
            new KyrolusNotFoundException("Order", "42"),
            new Exception("totally unclassified")
        };

    [Theory(DisplayName = "Middleware and the matching native IExceptionHandler produce identical status code and body for the same exception")]
    [MemberData(nameof(SharedExceptionCases))]
    public async Task Middleware_And_Handler_Should_Produce_Identical_Response(Exception exception)
    {
        var dependencies = CreateDependencies();

        var middlewareContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        RequestDelegate next = _ => throw exception;
        var middleware = new ExceptionHandlingMiddleware(next, dependencies);
        await middleware.Invoke(middlewareContext);

        var handlerContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handler = new GeneralExceptionHandler(dependencies);
        await handler.TryHandleAsync(handlerContext, exception, CancellationToken.None);

        middlewareContext.Response.StatusCode.ShouldBe(handlerContext.Response.StatusCode);

        middlewareContext.Response.Body.Seek(0, SeekOrigin.Begin);
        handlerContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var middlewareJson = await new StreamReader(middlewareContext.Response.Body).ReadToEndAsync();
        var handlerJson = await new StreamReader(handlerContext.Response.Body).ReadToEndAsync();

        middlewareJson.ShouldBe(handlerJson);
    }

    [Theory(DisplayName = "The MVC exception filter also produces the identical status code as the middleware for the same exception")]
    [MemberData(nameof(SharedExceptionCases))]
    public async Task Filter_And_Middleware_Should_Produce_Identical_StatusCode(Exception exception)
    {
        var dependencies = CreateDependencies();

        var middlewareContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        RequestDelegate next = _ => throw exception;
        var middleware = new ExceptionHandlingMiddleware(next, dependencies);
        await middleware.Invoke(middlewareContext);

        var filterHttpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            filterHttpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var exceptionContext = new Microsoft.AspNetCore.Mvc.Filters.ExceptionContext(actionContext, [])
        {
            Exception = exception
        };
        var filter = new KyrolusExceptionFilter(dependencies);
        await filter.OnExceptionAsync(exceptionContext);

        middlewareContext.Response.StatusCode.ShouldBe(filterHttpContext.Response.StatusCode);
    }
}
