namespace KyrolusSous.Gateway.Yarp.Transforms;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using KyrolusSous.Gateway.Abstractions;

/// <summary>
/// YARP transform provider that short-circuits requests for routes configured with Maintenance Mode or Mock Responses.
/// When triggered, responses are served immediately at the gateway perimeter without any upstream proxy network calls.
/// </summary>
public sealed class KyrolusMockAndMaintenanceTransformProvider : ITransformProvider
{
    private const string DefaultMaintenanceMessage = "The requested service is temporarily undergoing scheduled maintenance. Please try again later.";

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata is null)
        {
            return;
        }

        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Maintenance.StatusCode, out var maintStatusStr) &&
            !string.IsNullOrWhiteSpace(maintStatusStr))
        {
            if (!int.TryParse(maintStatusStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sc) || sc < 100 || sc > 599)
            {
                context.Errors.Add(new ArgumentException($"Route '{context.Route?.RouteId}' has an invalid maintenance status code: '{maintStatusStr}'. Must be between 100 and 599."));
            }
        }

        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Maintenance.RetryAfterSeconds, out var retryStr) &&
            !string.IsNullOrWhiteSpace(retryStr))
        {
            if (!int.TryParse(retryStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retry) || retry < 0)
            {
                context.Errors.Add(new ArgumentException($"Route '{context.Route?.RouteId}' has an invalid Retry-After value: '{retryStr}'. Must be a non-negative integer."));
            }
        }

        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Mock.StatusCode, out var mockStatusStr) &&
            !string.IsNullOrWhiteSpace(mockStatusStr))
        {
            if (!int.TryParse(mockStatusStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sc) || sc < 100 || sc > 599)
            {
                context.Errors.Add(new ArgumentException($"Route '{context.Route?.RouteId}' has an invalid mock status code: '{mockStatusStr}'. Must be between 100 and 599."));
            }
        }
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches maintenance mode or mock response transforms if configured in route metadata.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata is null)
        {
            return;
        }

        // 1. Maintenance Mode takes precedence over Mock Response
        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Maintenance.Enabled, out var maintEnabled) &&
            string.Equals(maintEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            var statusCode = StatusCodes.Status503ServiceUnavailable;
            if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Maintenance.StatusCode, out var codeStr) &&
                int.TryParse(codeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode) &&
                parsedCode is >= 100 and <= 599)
            {
                statusCode = parsedCode;
            }

            var message = metadata.TryGetValue(KyrolusGatewayMetadataKeys.Maintenance.Message, out var msg) && !string.IsNullOrWhiteSpace(msg)
                ? msg
                : DefaultMaintenanceMessage;

            int? retryAfterSeconds = null;
            if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Maintenance.RetryAfterSeconds, out var retryStr) &&
                int.TryParse(retryStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRetry) &&
                parsedRetry >= 0)
            {
                retryAfterSeconds = parsedRetry;
            }

            var payloadObject = new
            {
                type = $"https://httpstatuses.com/{statusCode}",
                title = "Service Unavailable",
                status = statusCode,
                detail = message
            };

            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payloadObject);

            context.AddRequestTransform(async transformContext =>
            {
                if (transformContext.HttpContext.Response.HasStarted)
                {
                    return;
                }

                var response = transformContext.HttpContext.Response;
                response.StatusCode = statusCode;
                response.ContentType = "application/problem+json";

                if (retryAfterSeconds.HasValue)
                {
                    response.Headers["Retry-After"] = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
                }

                await response.Body.WriteAsync(payloadBytes, transformContext.HttpContext.RequestAborted);
            });

            return;
        }

        // 2. Mock Response
        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Mock.Enabled, out var mockEnabled) &&
            string.Equals(mockEnabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            var statusCode = StatusCodes.Status200OK;
            if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.Mock.StatusCode, out var codeStr) &&
                int.TryParse(codeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode) &&
                parsedCode is >= 100 and <= 599)
            {
                statusCode = parsedCode;
            }

            var contentType = metadata.TryGetValue(KyrolusGatewayMetadataKeys.Mock.ContentType, out var ct) && !string.IsNullOrWhiteSpace(ct)
                ? ct
                : "application/json";

            var body = metadata.TryGetValue(KyrolusGatewayMetadataKeys.Mock.ResponseBody, out var b) && b != null
                ? b
                : "{}";

            var bodyBytes = Encoding.UTF8.GetBytes(body);

            context.AddRequestTransform(async transformContext =>
            {
                if (transformContext.HttpContext.Response.HasStarted)
                {
                    return;
                }

                var response = transformContext.HttpContext.Response;
                response.StatusCode = statusCode;
                response.ContentType = contentType;
                await response.Body.WriteAsync(bodyBytes, transformContext.HttpContext.RequestAborted);
            });
        }
    }
}
