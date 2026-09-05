using KyrolusSous.CQRS.Abstractions.Models;
namespace KyrolusSous.CQRS.EF.Query;

public sealed class GetPagedQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusQueryHandler<GetPagedQuery<TResponse, TKey>, KyrolusPagedResult<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<KyrolusPagedResult<TResponse>> Handle(GetPagedQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        // Clamp caller-supplied paging so PageSize = int.MaxValue (or negative) can't force the
        // database to attempt to materialize an enormous or malformed result set.
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, KyrolusPagingLimits.MaxPageSize);

        if (query.IncludeDeleted || query.DeletedOnly)
        {
            var softRepo = TryResolveSoftRepository();
            if (softRepo is not null)
            {
                return await LoadIncludingDeletedAsync(softRepo, query, pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
            }

            if (query.DeletedOnly)
            {
                return new KyrolusPagedResult<TResponse>([], 0, pageNumber, pageSize);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        if (query.Selector is not null)
        {
            var spec = new KyrolusEfPagedQuerySpecification<TResponse>(
                new SpecificationInputs<TResponse, TResponse>(
                    Filter: query.Filter,
                    OrderBy: query.OrderBy,
                    AsNoTracking: query.AsNoTracking ?? false,
                    UseSplitQuery: query.UseSplitQuery ?? false,
                    IncludeDeleted: false,
                    Selector: query.Selector,
                    Includes: includes
                ),
                pageNumber,
                pageSize);
            var (projectedItems, projectedTotal) = await repo.GetPagedAsync(spec, cancellationToken);
            return new KyrolusPagedResult<TResponse>(projectedItems, projectedTotal, pageNumber, pageSize);
        }

        var specification = new KyrolusEfPagedQuerySpecification<TResponse>(
           new SpecificationInputs<TResponse, TResponse>(
                    Filter: query.Filter,
                    OrderBy: query.OrderBy,
                    AsNoTracking: query.AsNoTracking ?? false,
                    UseSplitQuery: query.UseSplitQuery ?? false,
                    IncludeDeleted: false,
                    Selector: query.Selector,
                    Includes: includes
                ),
            pageNumber,
            pageSize);

        var (items, total) = await repo.GetPagedWithDefaultsAsync(
            specification,
            filter: query.Filter,
            orderBy: query.OrderBy,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken: cancellationToken,
            includeExpressions: includes);

        return new KyrolusPagedResult<TResponse>(items, total, pageNumber, pageSize);
    }

    /// <remarks>
    /// Neither <c>GetAllIncludingDeletedAsync</c>
    /// nor <c>GetDeletedOnlyAsync</c> has a paged/limited overload - only these, which always
    /// materialize every matching row - so Skip/Take are applied afterwards, in memory, over the
    /// fully materialized, soft-delete-inclusive result set. Mirrors the same "no paged variant on the
    /// soft-delete repository" situation <c>GetSeekQueryHandler.LoadIncludingDeletedAsync</c> handles
    /// for the seek provider. query.Selector is rejected before reaching here (see the Selector guard
    /// below) for the same reason <c>GetAllQueryHandler</c> rejects it: neither soft-delete method has
    /// a projected overload either, so silently returning full entities instead of the caller's
    /// projection would be a silent-wrong-data outcome.
    /// </remarks>
    private static async Task<KyrolusPagedResult<TResponse>> LoadIncludingDeletedAsync(
        IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey> softRepo,
        GetPagedQuery<TResponse, TKey> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (query.Selector is not null)
        {
            throw new InvalidOperationException(
                "[Kyrolus CQRS] GetPagedQuery.Selector is not supported when browsing soft-deleted " +
                "records (IncludeDeleted/DeletedOnly) - drop the projection, or query without " +
                "IncludeDeleted/DeletedOnly.");
        }

        var graph = KyrolusIncludeMerge.MergeGraph(query.IncludeGraph, query.IncludeExpressions);
        var all = query.DeletedOnly
            ? await softRepo.GetDeletedOnlyAsync(query.Filter, query.OrderBy, query.IncludeProperties, graph, query.AsNoTracking, query.UseSplitQuery, cancellationToken).ConfigureAwait(false)
            : await softRepo.GetAllIncludingDeletedAsync(query.Filter, query.OrderBy, query.IncludeProperties, graph, query.AsNoTracking, query.UseSplitQuery, cancellationToken).ConfigureAwait(false);

        var items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new KyrolusPagedResult<TResponse>(items, all.Count, pageNumber, pageSize);
    }

    private IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>? TryResolveSoftRepository()
    {
        try
        {
            return unitOfWork.GetRepository<IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>>();
        }
        catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
        {
            return null;
        }
    }
}
