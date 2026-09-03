namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query handler executing <see cref="ElasticAutocompleteQuery{TDocument}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticAutocompleteQueryHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticAutocompleteQueryHandler<TDocument, TId>>? logger = null)
    : IKyrolusQueryHandler<ElasticAutocompleteQuery<TDocument>, IReadOnlyList<string>>
    where TDocument : class
{
    public async Task<IReadOnlyList<string>> Handle(ElasticAutocompleteQuery<TDocument> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Prefix))
        {
            return [];
        }

        var limit = Math.Clamp(query.MaxSuggestions, 1, 50);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing autocomplete on '{DocumentType}' field '{Field}' with prefix '{Prefix}'",
            typeof(TDocument).Name,
            query.TargetField,
            query.Prefix);

        var searchResult = await repository.SearchAsync(s => s
            .Size(limit)
            .Query(q => q
                .Prefix(p => p
                    .Field(new Field(query.TargetField))
                    .Value(query.Prefix))), cancellationToken).ConfigureAwait(false);

        if (searchResult.Documents.Count == 0 && query.Fuzzy)
        {
            searchResult = await repository.SearchAsync(s => s
                .Size(limit)
                .Query(q => q
                    .Match(m => m
                        .Field(new Field(query.TargetField))
                        .Query(query.Prefix)
                        .Fuzziness(new Fuzziness("AUTO")))), cancellationToken).ConfigureAwait(false);
        }

        var prop = typeof(TDocument).GetProperty(
            query.TargetField,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

        var suggestions = new List<string>();
        foreach (var doc in searchResult.Documents)
        {
            var value = prop?.GetValue(doc)?.ToString();
            if (!string.IsNullOrWhiteSpace(value) && !suggestions.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                suggestions.Add(value);
            }
        }

        return suggestions;
    }
}
