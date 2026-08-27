namespace KyrolusSous.OpenApi;

public sealed class KyrolusStandardErrorResponsesTransformer(KyrolusOpenApiOptions? options = null) : IOpenApiOperationTransformer
{
    private readonly KyrolusOpenApiOptions _options = options ?? new();

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();

        AddResponse(operation, "400", "Bad Request / Validation Error");
        AddResponse(operation, "401", "Unauthorized");
        AddResponse(operation, "403", "Forbidden");

        if (_options.IncludeNotFoundResponse)
        {
            AddResponse(operation, "404", "Not Found");
        }

        if (_options.IncludeUnprocessableEntityResponse)
        {
            AddResponse(operation, "422", "Unprocessable Entity");
        }

        AddResponse(operation, "500", "Internal Server Error");

        return Task.CompletedTask;
    }

    private void AddResponse(OpenApiOperation operation, string statusCode, string description)
    {
        if (operation.Responses is not null && !operation.Responses.ContainsKey(statusCode))
        {
            var response = new OpenApiResponse
            {
                Description = description
            };

            if (_options.IncludeProblemDetailsSchema)
            {
                response.Content ??= new Dictionary<string, OpenApiMediaType>();
                response.Content["application/problem+json"] = new OpenApiMediaType();
            }

            operation.Responses[statusCode] = response;
        }
    }
}
