using KyrolusSous.ExceptionHandling.Handlers;
using KyrolusSous.StaticFunctions;

namespace KyrolusSous.BaseRepositoryEF.Repository;

public class RepositoryAsync<TDbcontext, TEntity, TKey>(TDbcontext dbcontext) : IRepositoryAsync<TDbcontext, TEntity, TKey>
    where TEntity : class
    where TDbcontext : DbContext
    where TKey : IEquatable<TKey>
{
    internal DbSet<TEntity> dbSet = dbcontext.Set<TEntity>();
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? filter = null,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, List<string>? includeProperties = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = dbSet;
        if (filter != null)
            query = query.Where(filter);

        if (includeProperties != null)
            foreach (var includeProperty in includeProperties)
                query = query.Include(includeProperty);

        if (orderBy != null)
            return await orderBy(query).ToListAsync(cancellationToken: cancellationToken);

        return await query.ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(TKey id,
            List<string>? includeProperties = null, CancellationToken cancellationToken = default)
    {

        return (await GetAllAsync(x =>
         EF.Property<TKey>(x, nameof(id))!.Equals(id), null,
             includeProperties, cancellationToken)).FirstOrDefault();
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await dbSet.AddRangeAsync(entities, cancellationToken);
        return entities;
    }

    public async Task<TEntity?> UpdateAsync(TEntity entity)
    {
        var primaryKey = GetPrimaryKey(entity, dbcontext);
        var entityInDb = await dbSet.FindAsync(primaryKey) ??
         throw new NotFoundException(typeof(TEntity).Name, primaryKey.ToString() ?? string.Empty);
        UpdateEntityProperties(entity, entityInDb);
        return entityInDb;
    }

    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities)
    {
        var updatedEntities = new List<TEntity>();
        var notFoundEntities = new List<string>();

        foreach (var entity in entities)
        {
            var primaryKey = GetPrimaryKey(entity, dbcontext);
            var entityInDb = await dbSet.FindAsync(primaryKey);
            if (entityInDb == null)
            {
                notFoundEntities.Add(string.Join(", ", primaryKey));
                continue;
            }
            UpdateEntityProperties(entity, entityInDb);
            updatedEntities.Add(entityInDb);
        }

        if (notFoundEntities.Count != 0)
            throw new NotFoundException($"{typeof(TEntity).Name} not found for keys: {string.Join("; ", notFoundEntities)}");

        return updatedEntities;
    }

    public async Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates)
    {

        var entity = await dbSet.FindAsync(KyrolusRepoistoryHelpers<TEntity>.ConvertJsonElementArray(keyValues)) ??
         throw new NotFoundException(typeof(TEntity).Name, keyValues?.ToString() ?? string.Empty);

        foreach (var update in updates)
        {
            if (typeof(TEntity).GetProperty(update.Key) == null)
                throw new InvalidOperationException($"Property '{update.Key}' not found on entity '{typeof(TEntity).Name}'.");
            KyrolusRepoistoryHelpers<TEntity>.SetProperty(entity, update.Key, update.Value);
            dbcontext.Entry(entity).Property(update.Key).IsModified = true;
        }
        if (dbcontext.Entry(entity).State == EntityState.Detached)
            dbcontext.Attach(entity);
        return entity;
    }
    public async Task<bool> Remove(TEntity entity)
    {
        var primaryKey = GetPrimaryKey(entity, dbcontext);

        var entityInDb = await dbSet.FindAsync(primaryKey) ??
         throw new NotFoundException(typeof(TEntity).Name, primaryKey?.ToString() ?? string.Empty);
        dbSet.Remove(entityInDb);
        return true;
    }

    public async Task<bool> Remove(object?[]? keyValues)
    {
        var entityInDb = await dbSet.FindAsync(KyrolusRepoistoryHelpers<TEntity>.ConvertJsonElementArray(keyValues)) ??
         throw new NotFoundException(typeof(TEntity).Name, keyValues?.ToString() ?? string.Empty);
        dbSet.Remove(entityInDb);
        return true;
    }
    public async Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities)
    {
        var entitiesToRemove = new List<TEntity>();
        var notFoundEntities = new List<string>();
        foreach (var entity in entities)
        {
            var primaryKey = GetPrimaryKey(entity, dbcontext);
            var entityInDb = await dbSet.FindAsync(primaryKey);
            if (entityInDb == null)
            {
                notFoundEntities.Add(string.Join(", ", primaryKey.Select(x => x?.ToString())));
                continue;
            }
            entitiesToRemove.Add(entityInDb);
        }
        if (notFoundEntities.Count != 0)
            throw new NotFoundException($"{typeof(TEntity).Name} not found for keys: {string.Join("; ", notFoundEntities)}");
        dbSet.RemoveRange(entitiesToRemove);
        return true;
    }


    public async Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await dbSet.AnyAsync(filter, cancellationToken);
    }
    private async Task<TEntity?> FindEntityInDbAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var keyNames = GetPrimaryKeyNames(dbcontext);
        var keyValues = keyNames.Select(k => entity.GetType().GetProperty(k)?.GetValue(entity)).ToArray();

        return await dbSet
            .Where(e => keyNames.Zip(keyValues, (key, value) => EF.Property<object>(e, key).Equals(value))
                                .Aggregate((a, b) => a && b))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<TEntity?> FindEntityInDbByIdAsync(TKey[] id, CancellationToken cancellationToken)
    {
        var keyNames = GetPrimaryKeyNames(dbcontext);
        if (keyNames.Length != id.Length)
            throw new ArgumentException("The number of provided key values does not match the primary key count.");

        var keyValues = keyNames.Select(k => id[keyNames.ToList().IndexOf(k)]).ToArray();

        return await dbSet.Where(e => keyNames.Zip(keyValues, (key, value) => EF.Property<TKey>(e, key).Equals(value))
                                               .Aggregate((a, b) => a && b))
                            .SingleOrDefaultAsync(cancellationToken);
    }
    private void UpdateEntityProperties(TEntity sourceEntity, TEntity targetEntity)
    {
        foreach (var property in dbcontext.Entry(sourceEntity).Properties)
            if (!Equals(property.CurrentValue, property.OriginalValue))
            {
                KyrolusRepoistoryHelpers<TEntity>.SetProperty(targetEntity, property.Metadata.Name, property.CurrentValue!);
                dbcontext.Entry(targetEntity).Property(property.Metadata.Name).IsModified = true;
            }
    }
    private static object?[] GetPrimaryKey(TEntity entity, DbContext context)
    {
        return [.. GetPrimaryKeyNames(context).Select(k => entity.GetType().GetProperty(k)?.GetValue(entity))];
    }

    private static string[] GetPrimaryKeyNames(DbContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type '{typeof(TEntity).Name}' not found in the model.");
        var primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Primary key not found for entity type '{typeof(TEntity).Name}'.");
        return [.. primaryKey.Properties.Select(x => x.Name)];
    }
}
