using KyrolusSous.CQRS.Abstractions.Security;

namespace KyrolusSous.CQRS.Elasticsearch.Query;

/// <summary>
/// Generic CQRS query handler executing <see cref="ElasticSearchQuery{TDocument}"/> using <see cref="IKyrolusElasticRepository{TDocument, TId}"/>.
/// </summary>
public sealed class ElasticSearchQueryHandler<TDocument, TId>(
    IKyrolusElasticRepository<TDocument, TId> repository,
    ILogger<ElasticSearchQueryHandler<TDocument, TId>>? logger = null)
    : IKyrolusQueryHandler<ElasticSearchQuery<TDocument>, KyrolusSearchResult<TDocument>>
    where TDocument : class
{
    public async Task<KyrolusSearchResult<TDocument>> Handle(ElasticSearchQuery<TDocument> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Opt-in guard, same shape as the write-side IKyrolusPropertyUpdateRequest.AllowedProperties
        // check: a null AllowedFields (the default) is unrestricted, so existing callers are unaffected.
        if (query.AllowedFields is { } allowedFields)
        {
            if (query.Fields is { Count: > 0 } fieldsToCheck)
            {
                foreach (var field in fieldsToCheck)
                {
                    EnsureFieldAllowed(field, allowedFields);
                }
            }

            if (!string.IsNullOrWhiteSpace(query.SortField))
            {
                EnsureFieldAllowed(query.SortField, allowedFields);
            }
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 1000);

        logger?.LogDebug(
            "[Kyrolus CQRS Elasticsearch] Executing search on '{DocumentType}' with term '{SearchTerm}' (Page {Page}, Size {PageSize})",
            typeof(TDocument).Name,
            query.SearchTerm ?? "(all)",
            page,
            pageSize);

        return await repository.SmartSearchAsync(builder =>
        {
            builder.Paginate(page, pageSize);

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                if (query.Fields is { Count: > 0 } fields)
                {
                    builder.Search(query.SearchTerm, fields.ToArray());
                }
                else
                {
                    builder.Search(query.SearchTerm, Array.Empty<string>());
                }

                if (query.EnableFuzzy)
                {
                    builder.Fuzzy(query.Fuzziness);
                }
            }

            if (!string.IsNullOrWhiteSpace(query.SortField))
            {
                builder.OrderBy(query.SortField, query.SortDescending);
            }

            if (query.HighlightFields is { Count: > 0 } highlights)
            {
                foreach (var highlight in highlights)
                {
                    builder.Highlight(highlight);
                }
            }

            query.CustomConfigure?.Invoke(builder);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Case-insensitive membership check, matching <c>KyrolusPropertyAllowListBehavior</c>'s own
    /// precedent: a caller could otherwise resubmit an allow-listed field name in different casing to
    /// bypass an ordinally-cased comparison.
    /// </summary>
    private static void EnsureFieldAllowed(string fieldName, IReadOnlySet<string> allowedFields)
    {
        foreach (var candidate in allowedFields)
        {
            if (string.Equals(candidate, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new KyrolusSecurityException(
            $"[Kyrolus CQRS Security] Field '{fieldName}' is not in the allow-list for {nameof(ElasticSearchQuery<TDocument>)}.");
    }
}
