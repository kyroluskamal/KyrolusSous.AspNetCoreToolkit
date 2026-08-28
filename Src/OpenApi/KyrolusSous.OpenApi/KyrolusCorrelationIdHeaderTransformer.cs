namespace KyrolusSous.OpenApi;

public sealed class KyrolusCorrelationIdHeaderTransformer(string headerName = "X-Correlation-ID") : IOpenApiOperationTransformer
{
    private readonly string _headerName = headerName;

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Parameters ??= [];

        var exists = operation.Parameters.Any(p => string.Equals(p.Name, _headerName, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = _headerName,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional correlation ID for end-to-end distributed tracing.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }

        return Task.CompletedTask;
    }
}
