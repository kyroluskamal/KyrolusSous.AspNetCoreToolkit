global using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.BaseRepositoryMarten.Repository;
using FluentValidation;
using Marten.Exceptions;
using Npgsql;

namespace KyrolusSous.BaseRepositoryMarten.UnitOfWork;

public class UnitOfWork<TDocument>(TDocument _session) : IUnitOfWork<TDocument>
    where TDocument : class
{
    private bool _disposed = false;

    private readonly IDocumentSession session = (IDocumentSession)_session;

    private readonly Dictionary<Type, object> _repositories = new();

    public IRepositoryAsync<TDocument, TEntity, TKey> Repository<TEntity, TKey>()
        where TEntity : class
        where TKey : IEquatable<TKey>
    {
        var type = typeof(TEntity);
        if (!_repositories.ContainsKey(type))
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(_session));
            }
            var repositoryInstance = new RepositoryMartenAsnc<TDocument, TEntity, TKey>(_session);
            _repositories[type] = repositoryInstance;
        }
        return (IRepositoryAsync<TDocument, TEntity, TKey>)_repositories[type];
    }

    public int Save()
    {
        session.SaveChangesAsync().GetAwaiter().GetResult();
        return 1;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                session?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (session != null)
        {
            await session.DisposeAsync();
        }

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    public async Task<int> SaveAsync<T>()
    {
        try
        {
            await session.SaveChangesAsync();
            return 1;
        }
        catch (DocumentAlreadyExistsException ex) when (ex.InnerException is PostgresException pgEx && PostgresErrorCodes.UniqueViolation == pgEx.SqlState)
        {
            string violatedProperty = ExtractPropertyFromConstraintName(pgEx.ConstraintName ?? "UnknownConstraint");

            throw new ValidationException([new ValidationFailure(violatedProperty, $"There is a {typeof(T).Name} with the same <<< {violatedProperty} >>> in the database. You can not dublicate it.")]);
        }

    }

    ~UnitOfWork()
    {
        Dispose(false);
    }

    private string ExtractPropertyFromConstraintName(string constraintName)
    {

        var parts = constraintName.Split('_');
        return CapitalizeFirstLetter(parts.Length > 2 ? parts[^1] : "UnknownProperty");
    }
    public string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1);
    }

}