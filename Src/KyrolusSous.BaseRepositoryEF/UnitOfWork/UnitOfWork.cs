using KyrolusSous.IRespositoryInterfaces.IRepository;
using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;

namespace KyrolusSous.BaseRepositoryEF.UnitOfWork;

public class UnitOfWork<TDbcontext>(TDbcontext dbcontext) : IUnitOfWork<TDbcontext>
   where TDbcontext : DbContext
{
    private readonly Dictionary<Type, object> _repositories = [];
    private bool _disposed = false;
    public IRepositoryAsync<TDbcontext, TEntity, TKey> Repository<TEntity, TKey>()
            where TEntity : class
            where TKey : IEquatable<TKey>
    {
        var type = typeof(TEntity);
        if (!_repositories.TryGetValue(type, out object? value))
        {
            var repositoryInstance = new RepositoryAsync<TDbcontext, TEntity, TKey>(dbcontext);
            value = repositoryInstance;
            _repositories[type] = value;
        }
        return (IRepositoryAsync<TDbcontext, TEntity, TKey>)value;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                dbcontext.Dispose();
            }
            _disposed = true;
        }
    }
    public Task<int> SaveAsync<T>()
    {
        return dbcontext.SaveChangesAsync();
    }

    public int Save()
    {
        return dbcontext.SaveChanges();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await dbcontext.DisposeAsync();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

}
