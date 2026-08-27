namespace KyrolusSous.OpenApi;

/// <summary>
/// Transformer that detects rate limiting metadata on operations and documents HTTP 429 Too Many Requests.
/// </summary>
public sealed class KyrolusRateLimitingResponseTransformer(KyrolusOpenApiOptions? options = null) : IOpenApiOperationTransformer
{
    private readonly KyrolusOpenApiOptions _options = options ?? new();

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata is null || metadata.Count == 0)
        {
            return Task.CompletedTask;
        }

        var isExplicitlyDisabled = metadata.Any(m =>
            m.GetType().Name.Contains("DisableRateLimit", StringComparison.OrdinalIgnoreCase));

        if (isExplicitlyDisabled)
        {
            return Task.CompletedTask;
        }

        var hasRateLimiting = metadata.Any(m =>
            m.GetType().Name.Contains("RateLimit", StringComparison.OrdinalIgnoreCase));

        if (hasRateLimiting)
        {
            operation.Responses ??= new OpenApiResponses();

            if (!operation.Responses.ContainsKey("429"))
            {
                var response = new OpenApiResponse
                {
                    Description = "Too Many Requests - The client has sent too many requests in a given amount of time."
                };

                if (_options.IncludeProblemDetailsSchema)
                {
                    response.Content ??= new Dictionary<string, OpenApiMediaType>();
                    response.Content["application/problem+json"] = new OpenApiMediaType();
                }

                operation.Responses["429"] = response;
            }
        }

        return Task.CompletedTask;
    }
}
