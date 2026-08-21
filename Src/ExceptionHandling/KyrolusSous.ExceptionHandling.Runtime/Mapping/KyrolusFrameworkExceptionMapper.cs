namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusFrameworkExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => 0;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        KyrolusExceptionMapping? candidate = exception switch
        {
            UnauthorizedAccessException => Create(HttpStatusCode.Unauthorized, KyrolusErrorCodes.Unauthorized, "Unauthorized"),
            AuthenticationException => Create(HttpStatusCode.Unauthorized, KyrolusErrorCodes.Unauthorized, "Unauthorized"),
            KeyNotFoundException => Create(HttpStatusCode.NotFound, KyrolusErrorCodes.NotFound, "Not found"),
            TimeoutException => Create(HttpStatusCode.GatewayTimeout, KyrolusErrorCodes.Timeout, "Timeout", isTransient: true),
            TaskCanceledException => Create(HttpStatusCode.RequestTimeout, KyrolusErrorCodes.Cancelled, "Request cancelled", isTransient: true),
            OperationCanceledException => Create(HttpStatusCode.RequestTimeout, KyrolusErrorCodes.Cancelled, "Request cancelled", isTransient: true),
            HttpRequestException httpEx when httpEx.StatusCode.HasValue => Create(httpEx.StatusCode.Value, KyrolusErrorCodes.ExternalService, "External service error", isTransient: true),
            HttpRequestException => Create(HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, "External service error", isTransient: true),
            SocketException => Create(HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, "External service error", isTransient: true),
            JsonException => Create(HttpStatusCode.BadRequest, KyrolusErrorCodes.InvalidJson, "Invalid JSON"),
            ArgumentException => Create(HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, "Bad request"),
            NotSupportedException => Create(HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, "Bad request"),
            _ => null
        };

        if (candidate is null)
        {
            mapping = null!;
            return false;
        }

        mapping = candidate with
        {
            Error = candidate.Error with { Detail = exception.Message, TraceId = context.TraceId }
        };
        return true;
    }

    private static KyrolusExceptionMapping Create(HttpStatusCode statusCode, string code, string title, bool isTransient = false)
    {
        return new KyrolusExceptionMapping(
            new KyrolusErrorEnvelope(code, title),
            statusCode,
            isTransient);
    }
}
