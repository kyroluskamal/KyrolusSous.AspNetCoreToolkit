using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Runtime;
using KyrolusSous.ExceptionHandling.Runtime.Interfaces;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusErrorResult(
    KyrolusExceptionMapping mapping,
    IKyrolusErrorResponseWriter writer,
    KyrolusHttpErrorContextFactory contextFactory,
    Exception? exception = null) : IResult
{
    private readonly KyrolusExceptionMapping mapping = mapping;
    private readonly IKyrolusErrorResponseWriter writer = writer;
    private readonly KyrolusHttpErrorContextFactory contextFactory = contextFactory;
    private readonly Exception? exception = exception;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var errorContext = contextFactory.Create(exception ?? new Exception(mapping.Error.Code));
        return writer.WriteAsync(httpContext, mapping, errorContext, httpContext.RequestAborted);
    }
}
