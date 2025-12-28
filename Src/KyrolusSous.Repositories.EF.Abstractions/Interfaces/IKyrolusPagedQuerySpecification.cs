namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusPagedQuerySpecification<TEntity, TResult> : IKyrolusQuerySpecification<TEntity, TResult>
{
    int PageNumber { get; }
    int PageSize { get; }
}
