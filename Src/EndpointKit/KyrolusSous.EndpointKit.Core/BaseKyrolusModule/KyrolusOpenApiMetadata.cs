using KyrolusSous.CQRS.Abstractions.Models;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public static class KyrolusOpenApiMetadata
{
    public static RouteHandlerBuilder ApplyOpenApi<TResponse>(
        this RouteHandlerBuilder builder,
        IKyrolusApiConfig<TResponse> config,
        EndpointNames endpoint,
        Type? overrideResponseType = null)
        where TResponse : class
    {
        var responses = ResolveResponses(config, endpoint, overrideResponseType);
        foreach (var response in responses)
        {
            builder.Produces(response.StatusCode, response.ResponseType, response.ContentType);
        }

        builder.WithMetadata(new KyrolusOpenApiOperationMetadata(BuildOperationId(config, endpoint), endpoint));

        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        var summary = endpointConfig?.Summary ?? $"{endpoint} {config.ApiName}";
        builder.WithSummary(summary);

        if (!string.IsNullOrWhiteSpace(endpointConfig?.Description))
        {
            builder.WithDescription(endpointConfig.Description);
        }

        return builder;
    }

    private static IReadOnlyList<KyrolusOpenApiResponse> ResolveResponses<TResponse>(
        IKyrolusApiConfig<TResponse> config,
        EndpointNames endpoint,
        Type? overrideResponseType)
        where TResponse : class
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        if (endpointConfig?.Responses is { Count: > 0 })
        {
            return endpointConfig.Responses.ToList();
        }

        if (config.DefaultResponses is { Count: > 0 })
        {
            return [.. config.DefaultResponses];
        }

        return BuildDefaultResponses(config, endpoint, overrideResponseType);
    }

    private static IReadOnlyList<KyrolusOpenApiResponse> BuildDefaultResponses<TResponse>(
        IKyrolusApiConfig<TResponse> config,
        EndpointNames endpoint,
        Type? overrideResponseType)
        where TResponse : class
    {
        var responses = new List<KyrolusOpenApiResponse>();
        var successStatus = endpoint is EndpointNames.Add or EndpointNames.AddRange
            ? StatusCodes.Status201Created
            : StatusCodes.Status200OK;
        var successType = overrideResponseType ?? ResolveSuccessType(config, endpoint);

        // HEAD returns no body, just status
        if (endpoint is EndpointNames.Head)
        {
            responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status200OK, null));
            responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status404NotFound, null));
        }
        else
        {
            responses.Add(new KyrolusOpenApiResponse(successStatus, successType));
        }

        if (endpoint is EndpointNames.GetById or EndpointNames.Head)
        {
            if (endpoint != EndpointNames.Head) // Already added for HEAD
            {
                responses.Add(ProblemResponse(StatusCodes.Status404NotFound));
            }
        }

        if (endpoint != EndpointNames.Head)
        {
            responses.Add(ProblemResponse(StatusCodes.Status400BadRequest));
        }

        if (RequireAuthorization(config, endpoint))
        {
            responses.Add(ProblemResponse(StatusCodes.Status401Unauthorized));
            responses.Add(ProblemResponse(StatusCodes.Status403Forbidden));
        }

        if (HasRateLimitPolicy(config, endpoint))
        {
            responses.Add(ProblemResponse(StatusCodes.Status429TooManyRequests));
        }

        return responses;
    }

    private static KyrolusOpenApiResponse ProblemResponse(int statusCode)
        => new(statusCode, typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), "application/problem+json");

    private static bool RequireAuthorization<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        return endpointConfig?.Authorize ?? config.AuthorizeAllEndpoints;
    }

    private static Type ResolveSuccessType<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        var viewModelType = ResolveViewModelType(config, endpoint);
        return endpoint switch
        {
            EndpointNames.GetAll or EndpointNames.Query or EndpointNames.GetDeleted => typeof(IEnumerable<>).MakeGenericType(viewModelType),
            EndpointNames.AddRange or EndpointNames.UpdateRange or EndpointNames.BulkUpsert => typeof(IEnumerable<>).MakeGenericType(viewModelType),
            EndpointNames.BulkUpdate or EndpointNames.BulkDelete or EndpointNames.BulkPatch => typeof(int),
            EndpointNames.Count => typeof(long),
            EndpointNames.Seek or EndpointNames.QuerySeek => typeof(KyrolusSeekResult<>).MakeGenericType(viewModelType),
            EndpointNames.Delete or EndpointNames.DeleteRange or EndpointNames.Restore => typeof(bool),
            EndpointNames.Batch => typeof(object), // Batch response type is set by route mapper
            EndpointNames.Paged or EndpointNames.QueryPaged => typeof(IEnumerable<>).MakeGenericType(viewModelType),
            _ => viewModelType
        };
    }

    private static Type ResolveViewModelType<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        if (endpointConfig?.ViewModelType is not null)
        {
            return endpointConfig.ViewModelType;
        }

        return config.ViewModelType ?? typeof(TResponse);
    }

    private static bool HasRateLimitPolicy<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        var policy = endpointConfig?.RateLimitPolicy ?? config.RateLimitPolicy;
        return !string.IsNullOrWhiteSpace(policy);
    }

    private static string BuildOperationId<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        var prefix = string.IsNullOrWhiteSpace(config.ApiName) ? config.Route : config.ApiName;
        var safePrefix = NormalizeOperationIdPart(prefix);
        return $"{safePrefix}_{endpoint}";
    }

    private static string NormalizeOperationIdPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "KyrolusApi";
        var buffer = new char[value.Length];
        var index = 0;
        foreach (var ch in value)
        {
            buffer[index++] = char.IsLetterOrDigit(ch) ? ch : '_';
        }
        return new string(buffer, 0, index);
    }

    internal static void ApplyParameterDocs(OpenApiOperation operation, EndpointNames endpoint)
    {
        if (operation.Parameters is null || operation.Parameters.Count == 0) return;
        foreach (var parameter in operation.Parameters)
        {
            if (parameter is null || string.IsNullOrWhiteSpace(parameter.Name)) continue;
            parameter.Description ??= parameter.Name switch
            {
                "filter" => "Filter expression. Supports in/between/isnull/notnull/any/all, parentheses, ',' (AND) and '|' (OR).",
                "includedProps" => "Comma-separated include paths.",
                "includeGraph" => "Include graph paths (comma-separated).",
                "fields" => "Comma-separated select fields.",
                "cacheable" => "Override cache policy for this request.",
                "includeDeleted" => "Include soft-deleted records.",
                "pageNumber" => "Page number (1-based).",
                "pageSize" => "Page size.",
                "cursor" => "Seek cursor token from previous response.",
                "includeTotalCount" => "Include total count in seek response.",
                "descending" => "Seek in descending order.",
                _ => null
            };
        }

        if (endpoint is EndpointNames.Query or EndpointNames.QueryPaged or EndpointNames.QuerySeek)
        {
            operation.Description ??= "QueryRequest supports filters, ordering, includes, fields, and includeGraph.";
        }
    }

    internal static void ApplyRequestExamples(OpenApiOperation operation, EndpointNames endpoint)
    {
        if (operation.RequestBody?.Content is null) return;
        if (endpoint is not (EndpointNames.Query or EndpointNames.QueryPaged or EndpointNames.QuerySeek)) return;
        if (!operation.RequestBody.Content.TryGetValue("application/json", out var content)) return;

        var examplePayload = new JsonObject
        {
            ["filters"] = new JsonArray
            {
                new JsonObject
                {
                    ["property"] = JsonValue.Create("status"),
                    ["operator"] = JsonValue.Create("eq"),
                    ["value"] = JsonValue.Create("active")
                }
            },
            ["orderBy"] = new JsonArray
            {
                new JsonObject
                {
                    ["property"] = JsonValue.Create("createdAt"),
                    ["desc"] = JsonValue.Create(true)
                }
            },
            ["includes"] = new JsonArray { JsonValue.Create("items") },
            ["fields"] = new JsonArray { JsonValue.Create("id"), JsonValue.Create("name") },
            ["asNoTracking"] = JsonValue.Create(true),
            ["useSplitQuery"] = JsonValue.Create(false),
            ["includeGraph"] = new JsonArray { JsonValue.Create("items.details") }
        };

        content.Example ??= endpoint is EndpointNames.Query
            ? examplePayload
            : new JsonObject { ["request"] = examplePayload };
    }
}
