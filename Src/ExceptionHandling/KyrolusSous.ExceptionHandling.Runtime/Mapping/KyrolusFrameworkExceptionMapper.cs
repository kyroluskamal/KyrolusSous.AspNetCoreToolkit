namespace KyrolusSous.ExceptionHandling.Runtime.Mapping;

public sealed class KyrolusFrameworkExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => 0;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        (HttpStatusCode StatusCode, string Code, string Title, bool IsTransient, bool ShouldLog)? candidate = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, KyrolusErrorCodes.Unauthorized, "Unauthorized access", false, false),
            AuthenticationException => (HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, "SSL authentication failed", true, true),
            KeyNotFoundException => (HttpStatusCode.NotFound, KyrolusErrorCodes.NotFound, "Resource key not found", false, false),
            TimeoutException => (HttpStatusCode.GatewayTimeout, KyrolusErrorCodes.Timeout, "Operation timeout", true, true),
            TaskCanceledException => (HttpStatusCode.RequestTimeout, KyrolusErrorCodes.Cancelled, "Request cancelled", true, false),
            OperationCanceledException => (HttpStatusCode.RequestTimeout, KyrolusErrorCodes.Cancelled, "Operation cancelled", true, false),
            HttpRequestException httpEx when httpEx.StatusCode.HasValue => (httpEx.StatusCode.Value, KyrolusErrorCodes.ExternalService, "External HTTP request failed", true, true),
            HttpRequestException => (HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, "Upstream HTTP service error", true, true),
            SocketException => (HttpStatusCode.BadGateway, KyrolusErrorCodes.ExternalService, "Network connection failed", true, true),
            JsonException => (HttpStatusCode.BadRequest, KyrolusErrorCodes.InvalidJson, "Invalid JSON payload", false, false),
            CultureNotFoundException => (HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, "Invalid culture", false, false),
            ArgumentException => (HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, "Invalid argument", false, false),
            FormatException => (HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, "Invalid format", false, false),
            NotImplementedException => (HttpStatusCode.NotImplemented, KyrolusErrorCodes.InternalError, "Feature not implemented", false, true),
            NotSupportedException => (HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, "Operation not supported", false, false),
            _ => null
        };

        if (candidate is null)
        {
            mapping = null!;
            return false;
        }

        var (statusCode, code, title, isTransient, shouldLog) = candidate.Value;

        mapping = KyrolusExceptionMapping.Create(
            code: code,
            title: title,
            statusCode: statusCode,
            errors: (exception as IKyrolusExceptionWithErrors)?.GetErrors(),
            detail: exception.Message,
            traceId: context?.TraceId,
            metadata: KyrolusMetadataExtractor.Extract(exception))
            .AsTransient(isTransient)
            .WithLogging(shouldLog);

        return true;
    }
}
