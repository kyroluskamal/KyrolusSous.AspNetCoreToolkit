using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using Marten;

namespace KyrolusSous.CQRS.Marten.Query;

/// <summary>
/// Handles <see cref="MartenSpecificationQuery{TEntity}"/> by executing the specification against the Marten repository.
/// </summary>
public class MartenSpecificationQueryHandler<TSession, TEntity, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<MartenSpecificationQuery<TEntity>, IReadOnlyList<TEntity>>
    where TSession : class, IDocumentSession
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    private readonly IKyrolusMartenUnitOfWork<TSession> _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async Task<IReadOnlyList<TEntity>> Handle(MartenSpecificationQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var repo = _unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>>();
        var options = new MartenQueryOptions<TEntity>(
            TenantId: query.TenantId,
            Specification: query.Specification);

        var results = await repo.GetAllAsync(options, cancellationToken).ConfigureAwait(false);
        return results is IReadOnlyList<TEntity> list ? list : results.ToList();
    }
}
