namespace KyrolusSous.OpenApi;

/// <summary>
/// Transformer that sorts OpenAPI tags and operation tags alphabetically.
/// </summary>
public sealed class KyrolusTagOrderDocumentTransformer(KyrolusOpenApiOptions? options = null) : IOpenApiDocumentTransformer
{
    private readonly KyrolusOpenApiOptions _options = options ?? new();

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.SortTagsAlphabetically)
        {
            return Task.CompletedTask;
        }

        if (document.Tags is { Count: > 1 })
        {
            document.Tags = new HashSet<OpenApiTag>(
                document.Tags.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase));
        }

        if (document.Paths is not null)
        {
            foreach (var (_, pathItem) in document.Paths)
            {
                if (pathItem.Operations is not null)
                {
                    foreach (var (_, operation) in pathItem.Operations)
                    {
                        if (operation.Tags is { Count: > 1 })
                        {
                            operation.Tags = new HashSet<OpenApiTagReference>(
                                operation.Tags.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase));
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
