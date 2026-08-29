using System.Net;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

namespace KyrolusSous.Payments.Abstractions;

public class KyrolusPaymentException : KyrolusException
{
    public string? ProviderName { get; }

    public KyrolusPaymentException(
        string message,
        string? providerName = null,
        string? detail = null,
        Exception? innerException = null)
        : base(
            HttpStatusCode.BadRequest,
            "PAYMENT_ERROR",
            message,
            detail,
            metadata: providerName != null ? new Dictionary<string, object?> { ["providerName"] = providerName } : null,
            innerException: innerException)
    {
        ProviderName = providerName;
    }
}

public class KyrolusPaymentGatewayException : KyrolusException
{
    public string ProviderName { get; }
    public string? ErrorCode { get; }

    public KyrolusPaymentGatewayException(
        string providerName,
        string message,
        string? errorCode = null,
        string? detail = null,
        Exception? innerException = null)
        : base(
            HttpStatusCode.BadGateway,
            errorCode ?? "EXTERNAL_SERVICE_ERROR",
            $"{providerName} Gateway Failure: {message}",
            detail,
            metadata: new Dictionary<string, object?>
            {
                ["providerName"] = providerName,
                ["gatewayErrorCode"] = errorCode
            },
            isTransient: true,
            shouldLog: true,
            innerException: innerException)
    {
        ProviderName = providerName;
        ErrorCode = errorCode;
    }
}

public class KyrolusPaymentNotFoundException : KyrolusException
{
    public string ResourceId { get; }
    public string? ProviderName { get; }

    public KyrolusPaymentNotFoundException(string transactionOrSubscriptionId, string? providerName = null)
        : base(
            HttpStatusCode.NotFound,
            "RESOURCE_NOT_FOUND",
            $"Payment resource '{transactionOrSubscriptionId}' was not found.",
            detail: providerName != null ? $"Provider: {providerName}" : null,
            metadata: new Dictionary<string, object?>
            {
                ["resourceId"] = transactionOrSubscriptionId,
                ["providerName"] = providerName
            })
    {
        ResourceId = transactionOrSubscriptionId;
        ProviderName = providerName;
    }
}
