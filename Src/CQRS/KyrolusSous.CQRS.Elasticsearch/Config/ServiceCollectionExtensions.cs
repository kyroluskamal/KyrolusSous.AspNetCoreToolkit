using KyrolusSous.CQRS.Elasticsearch.Command;
using KyrolusSous.CQRS.Elasticsearch.Projections;
using KyrolusSous.CQRS.Elasticsearch.Query;

namespace KyrolusSous.CQRS.Elasticsearch.Config;

/// <summary>
/// Service collection extensions for registering generic CQRS queries, commands, and read-model projectors for Elasticsearch.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers generic CQRS search queries, commands, and projector for a specific document type and identifier type.
    /// </summary>
    /// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
    /// <typeparam name="TId">The document identifier type.</typeparam>
    public static IServiceCollection AddKyrolusCqrsElasticsearch<TDocument, TId>(this IServiceCollection services)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(services);

        // Queries
        services.TryAddTransient<IKyrolusQueryHandler<ElasticSearchQuery<TDocument>, KyrolusSearchResult<TDocument>>,
            ElasticSearchQueryHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusQueryHandler<ElasticAutocompleteQuery<TDocument>, IReadOnlyList<string>>,
            ElasticAutocompleteQueryHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusQueryHandler<ElasticVectorSearchQuery<TDocument>, KyrolusSearchResult<TDocument>>,
            ElasticVectorSearchQueryHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusQueryHandler<ElasticHybridSearchQuery<TDocument>, KyrolusSearchResult<TDocument>>,
            ElasticHybridSearchQueryHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusQueryHandler<ElasticCountQuery<TDocument>, long>,
            ElasticCountQueryHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusQueryHandler<ElasticGetByIdQuery<TDocument, TId>, TDocument?>,
            ElasticGetByIdQueryHandler<TDocument, TId>>();

        // Commands
        services.TryAddTransient<IKyrolusCommandHandler<ElasticIndexDocumentCommand<TDocument, TId>, bool>,
            ElasticIndexDocumentCommandHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusCommandHandler<ElasticDeleteDocumentCommand<TDocument, TId>, bool>,
            ElasticDeleteDocumentCommandHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusCommandHandler<ElasticBulkIndexCommand<TDocument, TId>, KyrolusBulkResult>,
            ElasticBulkIndexCommandHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusCommandHandler<ElasticBulkDeleteCommand<TDocument, TId>, KyrolusBulkResult>,
            ElasticBulkDeleteCommandHandler<TDocument, TId>>();

        services.TryAddTransient<IKyrolusCommandHandler<ElasticUpdatePartialCommand<TDocument, TId>, bool>,
            ElasticUpdatePartialCommandHandler<TDocument, TId>>();

        // Read Model Projector
        services.TryAddTransient<IKyrolusReadModelProjector
<TDocument>,
            KyrolusElasticReadModelProjector<TDocument, TId>>();

        return services;
    }

    /// <summary>
    /// Registers generic CQRS search queries, commands, and projector for a specific document type with a string identifier.
    /// </summary>
    /// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
    public static IServiceCollection AddKyrolusCqrsElasticsearch<TDocument>(this IServiceCollection services)
        where TDocument : class
        => services.AddKyrolusCqrsElasticsearch<TDocument, string>();
}
