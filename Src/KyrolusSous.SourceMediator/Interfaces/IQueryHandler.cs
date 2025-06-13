namespace KyrolusSous.SourceMediator.Interfaces;
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse> where TResponse : notnull
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
