namespace KyrolusSous.CQRS.EF.Query;

/// <summary>
/// Handles <see cref="SpecificationQuery{TEntity, TResult}"/> by executing the specification against the EF repository.
/// </summary>
public class SpecificationQueryHandler<TDbContext, TEntity, TResult, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusQueryHandler<SpecificationQuery<TEntity, TResult>, IReadOnlyList<TResult>>
    where TDbContext : DbContext
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    private readonly IKyrolusUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async Task<IReadOnlyList<TResult>> Handle(SpecificationQuery<TEntity, TResult> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var repo = _unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbContext, TEntity, TKey>>();
        var results = await repo.QueryAsync(query.Specification, cancellationToken).ConfigureAwait(false);
        return results;
    }
}
