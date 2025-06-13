global using KyrolusSous.IRespositoryInterfaces.IRepository;
namespace KyrolusSous.IRespositoryInterfaces.IUnitOfWork;

public interface IUnitOfWork<TDbcontext> : IDisposable, IAsyncDisposable
where TDbcontext : class
{
    public IRepositoryAsync<TDbcontext, TEntity, TKey> Repository<TEntity, TKey>()
        where TEntity : class
        where TKey : IEquatable<TKey>;

    Task<int> SaveAsync<T>();
    int Save();
}
