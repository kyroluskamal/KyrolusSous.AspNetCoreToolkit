namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusExternalServiceException(string serviceName, string? detail = null, Exception? innerException = null) : KyrolusException(
        HttpStatusCode.BadGateway,
        KyrolusErrorCodes.ExternalService,
        $"{serviceName} failure",
        detail,
        null,
        new Dictionary<string, object?> { ["serviceName"] = serviceName },
        isTransient: true,
        shouldLog: true,
        innerException)
{
    public string ServiceName { get; } = serviceName;
}
