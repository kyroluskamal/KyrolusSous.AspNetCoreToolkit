namespace KyrolusSous.CQRS.Elasticsearch.Projections;

/// <summary>
/// Enterprise read-model projector synchronizing projected read-models directly into an Elasticsearch index.
/// Integrates automatically with <see cref="KyrolusReadModelProjectionBehavior{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TDocument">The projected read-model document type.</typeparam>
/// <typeparam name="TId">The identifier type for the document.</typeparam>
public class KyrolusElasticReadModelProjector<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<KyrolusElasticReadModelProjector<TDocument, TId>>? logger = null)
    : IReadModelProjector<TDocument>
    where TDocument : class
{
    public async Task ProjectAsync(TDocument model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var id = ExtractDocumentId(model);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Auto-projecting read model '{DocumentType}' with Id '{Id}' to Elasticsearch index '{IndexName}'",
            typeof(TDocument).Name,
            id,
            repository.IndexName);

        var success = await repository.AddAsync(model, id, cancellationToken).ConfigureAwait(false);
        if (!success)
        {
            logger?.LogWarning(
                "[Kyrolus CQRS Elasticsearch] Projection for '{DocumentType}' with Id '{Id}' returned false from Elasticsearch repository",
                typeof(TDocument).Name,
                id);
        }
    }

    private static TId ExtractDocumentId(TDocument model)
    {
        var type = typeof(TDocument);

        // Try 'Id', 'Key', or 'DocumentId'
        var prop = type.GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
            ?? type.GetProperty("Key", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
            ?? type.GetProperty($"{type.Name}Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

        if (prop is not null)
        {
            var value = prop.GetValue(model);
            if (value is TId typedId)
            {
                return typedId;
            }

            if (value is not null && typeof(TId) == typeof(string))
            {
                return (TId)(object)value.ToString()!;
            }
        }

        throw new InvalidOperationException(
            $"Could not extract identifier of type '{typeof(TId).Name}' from read model '{typeof(TDocument).Name}'. " +
            $"Ensure the read model declares a public property named 'Id' or matches '{typeof(TId).Name}'.");
    }
}
