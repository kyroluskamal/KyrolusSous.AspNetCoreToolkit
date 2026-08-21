namespace KyrolusSous.OpenApi;

public sealed class KyrolusStandardErrorResponsesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();

        if (!operation.Responses.ContainsKey("400"))
        {
            operation.Responses["400"] = new OpenApiResponse
            {
                Description = "Bad Request / Validation Error"
            };
        }

        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses["401"] = new OpenApiResponse
            {
                Description = "Unauthorized"
            };
        }

        if (!operation.Responses.ContainsKey("403"))
        {
            operation.Responses["403"] = new OpenApiResponse
            {
                Description = "Forbidden"
            };
        }

        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses["500"] = new OpenApiResponse
            {
                Description = "Internal Server Error"
            };
        }

        return Task.CompletedTask;
    }
}
