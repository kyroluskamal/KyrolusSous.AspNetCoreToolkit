using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed class KyrolusOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<KyrolusOpenApiOperationMetadata>()
            .FirstOrDefault();

        if (metadata is null)
        {
            return Task.CompletedTask;
        }

        operation.OperationId = metadata.OperationId;
        KyrolusOpenApiMetadata.ApplyParameterDocs(operation, metadata.Endpoint);
        KyrolusOpenApiMetadata.ApplyRequestExamples(operation, metadata.Endpoint);
        return Task.CompletedTask;
    }
}
