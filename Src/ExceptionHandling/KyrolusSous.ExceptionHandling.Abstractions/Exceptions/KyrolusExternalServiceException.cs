namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusExternalServiceException : KyrolusException
{
    public string ServiceName { get; }

    public KyrolusExternalServiceException(string serviceName, string? detail = null, Exception? innerException = null) 
        : base(
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
        ServiceName = serviceName;
    }
}
