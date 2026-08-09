namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;

/// <summary>
/// Provides custom OpenAPI schema generation capabilities for Kyrolus endpoints.
/// Implement this interface to customize how OpenAPI documentation is generated for your API.
/// </summary>
public interface IKyrolusOpenApiSchemaProvider
{
    /// <summary>
    /// Gets the OpenAPI description for the specified endpoint.
    /// </summary>
    /// <typeparam name="TResponse">The response entity type.</typeparam>
    /// <param name="config">The API configuration.</param>
    /// <param name="endpoint">The endpoint type.</param>
    /// <returns>The endpoint description.</returns>
    string? GetDescription<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class;

    /// <summary>
    /// Gets the OpenAPI summary for the specified endpoint.
    /// </summary>
    /// <typeparam name="TResponse">The response entity type.</typeparam>
    /// <param name="config">The API configuration.</param>
    /// <param name="endpoint">The endpoint type.</param>
    /// <returns>The endpoint summary.</returns>
    string? GetSummary<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class;

    /// <summary>
    /// Gets the OpenAPI tags for the specified endpoint.
    /// </summary>
    /// <typeparam name="TResponse">The response entity type.</typeparam>
    /// <param name="config">The API configuration.</param>
    /// <param name="endpoint">The endpoint type.</param>
    /// <returns>The list of tag names.</returns>
    IReadOnlyList<string>? GetTags<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class;

    /// <summary>
    /// Gets the operation ID for the specified endpoint.
    /// </summary>
    /// <typeparam name="TResponse">The response entity type.</typeparam>
    /// <param name="config">The API configuration.</param>
    /// <param name="endpoint">The endpoint type.</param>
    /// <returns>The operation ID.</returns>
    string? GetOperationId<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class;
}

/// <summary>
/// Default implementation of <see cref="IKyrolusOpenApiSchemaProvider"/> that provides
/// standard OpenAPI documentation for Kyrolus endpoints.
/// </summary>
public class KyrolusDefaultOpenApiSchemaProvider : IKyrolusOpenApiSchemaProvider
{
    /// <inheritdoc />
    public string? GetDescription<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        return endpoint switch
        {
            EndpointNames.GetAll => $"Retrieves all {config.ApiName ?? config.Route} entities.",
            EndpointNames.GetById => $"Retrieves a single {config.ApiName ?? config.Route} entity by its identifier.",
            EndpointNames.Head => $"Checks if a {config.ApiName ?? config.Route} entity exists by its identifier. Returns 200 OK if found, 404 Not Found otherwise.",
            EndpointNames.Count => $"Returns the total count of {config.ApiName ?? config.Route} entities matching the filter criteria.",
            EndpointNames.Add => $"Creates a new {config.ApiName ?? config.Route} entity.",
            EndpointNames.AddRange => $"Creates multiple {config.ApiName ?? config.Route} entities.",
            EndpointNames.Update => $"Updates an existing {config.ApiName ?? config.Route} entity.",
            EndpointNames.UpdateRange => $"Updates multiple {config.ApiName ?? config.Route} entities.",
            EndpointNames.Delete => $"Deletes a {config.ApiName ?? config.Route} entity by its identifier.",
            EndpointNames.DeleteRange => $"Deletes multiple {config.ApiName ?? config.Route} entities.",
            EndpointNames.Patch => $"Partially updates a {config.ApiName ?? config.Route} entity.",
            EndpointNames.Query => $"Queries {config.ApiName ?? config.Route} entities with advanced filter, sort, and projection options.",
            EndpointNames.Paged => $"Retrieves {config.ApiName ?? config.Route} entities with offset-based pagination.",
            EndpointNames.QueryPaged => $"Queries {config.ApiName ?? config.Route} entities with advanced options and offset-based pagination.",
            EndpointNames.Seek => $"Retrieves {config.ApiName ?? config.Route} entities with cursor-based (seek) pagination.",
            EndpointNames.QuerySeek => $"Queries {config.ApiName ?? config.Route} entities with advanced options and cursor-based pagination.",
            EndpointNames.BulkUpdate => $"Performs bulk update on {config.ApiName ?? config.Route} entities matching filter criteria.",
            EndpointNames.BulkDelete => $"Performs bulk delete on {config.ApiName ?? config.Route} entities matching filter criteria.",
            EndpointNames.BulkUpsert => $"Creates or updates multiple {config.ApiName ?? config.Route} entities.",
            EndpointNames.BulkPatch => $"Partially updates multiple {config.ApiName ?? config.Route} entities.",
            EndpointNames.GetDeleted => $"Retrieves soft-deleted {config.ApiName ?? config.Route} entities.",
            EndpointNames.Restore => $"Restores a soft-deleted {config.ApiName ?? config.Route} entity.",
            _ => null
        };
    }

    /// <inheritdoc />
    public string? GetSummary<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        var entityName = config.ApiName ?? config.Route ?? typeof(TResponse).Name;
        return endpoint switch
        {
            EndpointNames.GetAll => $"Get all {entityName}",
            EndpointNames.GetById => $"Get {entityName} by ID",
            EndpointNames.Head => $"Check {entityName} exists",
            EndpointNames.Count => $"Count {entityName}",
            EndpointNames.Add => $"Create {entityName}",
            EndpointNames.AddRange => $"Create {entityName} (batch)",
            EndpointNames.Update => $"Update {entityName}",
            EndpointNames.UpdateRange => $"Update {entityName} (batch)",
            EndpointNames.Delete => $"Delete {entityName}",
            EndpointNames.DeleteRange => $"Delete {entityName} (batch)",
            EndpointNames.Patch => $"Patch {entityName}",
            EndpointNames.Query => $"Query {entityName}",
            EndpointNames.Paged => $"Get {entityName} (paged)",
            EndpointNames.QueryPaged => $"Query {entityName} (paged)",
            EndpointNames.Seek => $"Get {entityName} (seek)",
            EndpointNames.QuerySeek => $"Query {entityName} (seek)",
            EndpointNames.BulkUpdate => $"Bulk update {entityName}",
            EndpointNames.BulkDelete => $"Bulk delete {entityName}",
            EndpointNames.BulkUpsert => $"Bulk upsert {entityName}",
            EndpointNames.BulkPatch => $"Bulk patch {entityName}",
            EndpointNames.GetDeleted => $"Get deleted {entityName}",
            EndpointNames.Restore => $"Restore {entityName}",
            _ => null
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<string>? GetTags<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class => null;

    /// <inheritdoc />
    public string? GetOperationId<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class => null;
}
