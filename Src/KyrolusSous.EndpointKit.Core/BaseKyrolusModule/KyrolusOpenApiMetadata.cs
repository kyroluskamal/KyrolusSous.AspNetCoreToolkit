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
            return config.DefaultResponses.ToList();
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
        responses.Add(new KyrolusOpenApiResponse(successStatus, successType));

        if (endpoint is EndpointNames.GetById)
        {
            responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status404NotFound));
        }

        responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status400BadRequest));

        if (RequireAuthorization(config, endpoint))
        {
            responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status401Unauthorized));
            responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status403Forbidden));
        }

        if (HasRateLimitPolicy(config, endpoint))
        {
            responses.Add(new KyrolusOpenApiResponse(StatusCodes.Status429TooManyRequests));
        }

        return responses;
    }

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
            EndpointNames.GetAll or EndpointNames.Query => typeof(IEnumerable<>).MakeGenericType(viewModelType),
            EndpointNames.AddRange or EndpointNames.UpdateRange => typeof(IEnumerable<>).MakeGenericType(viewModelType),
            EndpointNames.BulkUpdate or EndpointNames.BulkDelete => typeof(int),
            EndpointNames.Delete or EndpointNames.DeleteRange => typeof(bool),
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
}
