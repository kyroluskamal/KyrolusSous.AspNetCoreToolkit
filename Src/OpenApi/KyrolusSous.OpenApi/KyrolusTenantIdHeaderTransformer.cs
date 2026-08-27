namespace KyrolusSous.OpenApi;

/// <summary>
/// Transformer that adds the multi-tenant header (e.g. X-Tenant-Id) to OpenAPI operations.
/// </summary>
public sealed class KyrolusTenantIdHeaderTransformer(
    string headerName = "X-Tenant-Id",
    string description = "Tenant identifier for multi-tenant requests.") : IOpenApiOperationTransformer
{
    private readonly string _headerName = string.IsNullOrWhiteSpace(headerName) ? "X-Tenant-Id" : headerName;
    private readonly string _description = description;

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
                Description = _description
            });
        }

        return Task.CompletedTask;
    }
}
