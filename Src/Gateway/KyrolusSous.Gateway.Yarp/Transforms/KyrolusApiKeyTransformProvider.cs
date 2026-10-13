namespace KyrolusSous.Gateway.Yarp.Transforms;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using KyrolusSous.Gateway.Abstractions;

/// <summary>
/// YARP transform provider that enforces API key authentication at the edge perimeter.
/// Validates incoming API keys against a static allowlist or dynamic <see cref="IKyrolusApiKeyValidator"/>,
/// short-circuiting unauthorized requests with HTTP 401 ProblemDetails before reaching backend services.
/// </summary>
public sealed class KyrolusApiKeyTransformProvider : ITransformProvider
{
    private const string DefaultHeaderName = "X-Api-Key";

    private static readonly byte[] MissingKeyPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(new
    {
        type = "https://httpstatuses.com/401",
        title = "Unauthorized",
        status = StatusCodes.Status401Unauthorized,
        detail = "Missing required API key header or query parameter."
    });

    private static readonly byte[] InvalidKeyPayloadBytes = JsonSerializer.SerializeToUtf8Bytes(new
    {
        type = "https://httpstatuses.com/401",
        title = "Unauthorized",
        status = StatusCodes.Status401Unauthorized,
        detail = "Invalid or revoked API key."
    });

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata is null)
        {
            return;
        }

        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.ApiKey.Required, out var req) &&
            string.Equals(req, "true", StringComparison.OrdinalIgnoreCase))
        {
            if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.ApiKey.HeaderName, out var header) &&
                string.IsNullOrWhiteSpace(header))
            {
                context.Errors.Add(new ArgumentException($"Route '{context.Route?.RouteId}' specifies an empty API key header name."));
            }
        }
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches API key validation transform if configured in route metadata.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        var metadata = context.Route?.Metadata;
        if (metadata is null)
        {
            return;
        }

        if (!metadata.TryGetValue(KyrolusGatewayMetadataKeys.ApiKey.Required, out var required) ||
            !string.Equals(required, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var headerName = metadata.TryGetValue(KyrolusGatewayMetadataKeys.ApiKey.HeaderName, out var customHeader) && !string.IsNullOrWhiteSpace(customHeader)
            ? customHeader
            : DefaultHeaderName;

        string[]? allowedKeys = null;
        if (metadata.TryGetValue(KyrolusGatewayMetadataKeys.ApiKey.AllowedKeys, out var rawAllowed) && !string.IsNullOrWhiteSpace(rawAllowed))
        {
            allowedKeys = rawAllowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var request = transformContext.HttpContext.Request;

            // 1. Extract API key from custom/default header
            string? providedKey = null;
            if (request.Headers.TryGetValue(headerName, out var headerValues) && headerValues.Count > 0)
            {
                providedKey = headerValues[0];
            }

            // 2. Fallback to query parameter
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                if (request.Query.TryGetValue(headerName, out var queryValues) && queryValues.Count > 0)
                {
                    providedKey = queryValues[0];
                }
                else if (request.Query.TryGetValue("apiKey", out var apiKeyValues) && apiKeyValues.Count > 0)
                {
                    providedKey = apiKeyValues[0];
                }
                else if (request.Query.TryGetValue("api_key", out var apiKeySnakeValues) && apiKeySnakeValues.Count > 0)
                {
                    providedKey = apiKeySnakeValues[0];
                }
            }

            // If completely missing -> 401
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                await RejectWithUnauthorizedAsync(transformContext.HttpContext, MissingKeyPayloadBytes);
                return;
            }

            // 3. Check against static allowlist if configured (constant-time comparison)
            if (allowedKeys is { Length: > 0 })
            {
                var providedBytes = Encoding.UTF8.GetBytes(providedKey);
                var matched = false;

                foreach (var allowed in allowedKeys)
                {
                    var allowedBytes = Encoding.UTF8.GetBytes(allowed);
                    if (CryptographicOperations.FixedTimeEquals(providedBytes, allowedBytes))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    return; // Key is valid, allow request to proceed
                }
            }

            // 4. Check against dynamic validator if registered in DI
            var validator = transformContext.HttpContext.RequestServices.GetService<IKyrolusApiKeyValidator>();
            if (validator != null)
            {
                var isValid = await validator.ValidateAsync(providedKey, transformContext.HttpContext, transformContext.HttpContext.RequestAborted);
                if (isValid)
                {
                    return; // Dynamic validator accepted key, allow request to proceed
                }
            }

            // Neither static allowlist nor dynamic validator validated the key -> 401
            await RejectWithUnauthorizedAsync(transformContext.HttpContext, InvalidKeyPayloadBytes);
        });
    }

    private static async Task RejectWithUnauthorizedAsync(HttpContext httpContext, byte[] payload)
    {
        var response = httpContext.Response;
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.ContentType = "application/problem+json";
        await response.Body.WriteAsync(payload, httpContext.RequestAborted);
    }
}
