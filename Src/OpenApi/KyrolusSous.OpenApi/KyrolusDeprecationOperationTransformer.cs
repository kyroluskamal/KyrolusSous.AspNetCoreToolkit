namespace KyrolusSous.OpenApi;

/// <summary>
/// Transformer that marks operations as deprecated when decorated with <see cref="ObsoleteAttribute"/>
/// and appends deprecation details to the description.
/// </summary>
public sealed class KyrolusDeprecationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var obsolete = metadata?.OfType<ObsoleteAttribute>().FirstOrDefault();

        if (obsolete is null)
        {
            return Task.CompletedTask;
        }

        operation.Deprecated = true;

        var notice = !string.IsNullOrWhiteSpace(obsolete.Message)
            ? $"\n\n> ⚠️ **Deprecated:** {obsolete.Message}"
            : "\n\n> ⚠️ **Deprecated:** This endpoint is obsolete and may be removed in a future release.";

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? notice.TrimStart()
            : operation.Description + notice;

        return Task.CompletedTask;
    }
}
