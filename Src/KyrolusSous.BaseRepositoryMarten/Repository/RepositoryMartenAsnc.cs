using System.Text.Json;
using KyrolusSous.ExceptionHandling.Handlers;
using Marten.Patching;

namespace KyrolusSous.BaseRepositoryMarten.Repository;

public class RepositoryMartenAsnc<TDocument, TEntity, TKey>(TDocument _session)
    : IRepositoryAsync<TDocument, TEntity, TKey>
    where TDocument : class
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    private readonly IDocumentSession session = (IDocumentSession)_session;
    public async Task<IEnumerable<TEntity>> GetAllAsync(
           Expression<Func<TEntity, bool>>? filter = null,
           Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
           List<string>? includeProperties = null,
           CancellationToken cancellationToken = default)
    {
        IMartenQueryable<TEntity> query = session.Query<TEntity>();

        query = Filter(query, filter);
        var dictionaries = new Dictionary<string, object>();

        if (includeProperties != null) query = Include(query, includeProperties, dictionaries);

        if (orderBy != null) query = (IMartenQueryable<TEntity>)orderBy(query);

        var results = await query.ToListAsync(cancellationToken);

        PopulateNavigationProperties(results, dictionaries);

        return results;
    }
    public async Task<TEntity?> GetByIdAsync(TKey id, List<string>? includeProperties = null, CancellationToken cancellationToken = default)
    {

        var entityInDb = await session.LoadAsync<TEntity>(id, CancellationToken.None);
        if (entityInDb == null) return null!;
        if (includeProperties != null) await IncludePropertiesAsync([entityInDb], includeProperties);
        return await Task.FromResult(entityInDb);
    }
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        session.Store(entity);
        return await Task.FromResult(entity);
    }

    public async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        session.Store(entities.ToArray());
        return await Task.FromResult(entities);
    }

    public async Task<TEntity?> UpdateAsync(TEntity entity)
    {
        var idProperty = GetIdentityKeyProperty();
        var idValue = idProperty?.GetValue(entity) ?? throw new InvalidOperationException("Id value cannot be null.");
        var entityInDb = await session.LoadAsync<TEntity>((TKey)idValue);
        if (entityInDb == null) return null!;
        session.Store(entity);
        return entity;
    }

    public async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities)
    {
        var updatedEntities = new List<TEntity>();
        var missingEntities = new List<string>();
        foreach (var entity in entities)
        {
            var updatedEntity = await UpdateAsync(entity);
            if (updatedEntity == null)
            {
                var idProperty = GetIdentityKeyProperty();
                var idValue = idProperty?.GetValue(entity)?.ToString();
                if (!string.IsNullOrEmpty(idValue)) missingEntities.Add(idValue);
            }
            else updatedEntities.Add(updatedEntity);
        }
        if (missingEntities.Count != 0)
            throw new NotFoundException($"{typeof(TEntity).Name} not found for the following IDs: {string.Join(", ", missingEntities)}");
        return updatedEntities;
    }
    public async Task<TEntity?> PatchAsync(object?[]? keyValues, Dictionary<string, object> updates)
    {
        if (updates == null || updates.Count == 0)
            throw new ArgumentException("Updates cannot be null or empty.", nameof(updates));

        var key = KyrolusRepoistoryHelpers<TEntity>.ConvertJsonElementArray(keyValues)[0];
        var entityInDb = await session.LoadAsync<TEntity>(key!) ??
            throw new NotFoundException($"{typeof(TEntity).Name} not found for the following IDs: {string.Join(", ", keyValues?.ToString())}");
        var keyProperty = GetIdentityKeyProperty();
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Property(parameter, keyProperty?.Name!);
        var keyConverted = Convert.ChangeType(key, property.Type);
        var keyConstant = Expression.Constant(keyConverted, property.Type);
        var equalsExp = Expression.Equal(property, keyConstant);
        var lambda = Expression.Lambda<Func<TEntity, bool>>(equalsExp, parameter);
        var patchCommand = session.Patch(lambda);
        foreach (var update in updates)
        {
            object? convertedValue = update.Value;
            if (update.Value is JsonElement jsonElement)
            {
                convertedValue = KyrolusRepoistoryHelpers<TEntity>.ConvertJsonElement(jsonElement);
            }
            patchCommand.Set(update.Key, convertedValue);
            KyrolusRepoistoryHelpers<TEntity>.SetProperty(entityInDb, update.Key, value: convertedValue);
        }

        return entityInDb;
    }

    public async Task<bool> Remove(TEntity entity)
    {
        var idProperty = GetIdentityKeyProperty();
        var idValue = idProperty?.GetValue(entity) ?? throw new InvalidOperationException("Id value cannot be null.");
        var entityInDb = await session.LoadAsync<TEntity>((TKey)idValue);
        if (entityInDb == null) return false;
        session.Delete(entityInDb);
        return true;
    }

    public async Task<bool> Remove(object?[]? keyValues)
    {
        var key = KyrolusRepoistoryHelpers<TEntity>.ConvertJsonElementArray(keyValues)[0];

        var entity =
        await session.LoadAsync<TEntity>(key!) ?? throw new NotFoundException($"{typeof(TEntity).Name} not found for the following IDs: {string.Join(", ", keyValues?.ToString())}");
        session.Delete(entity);
        return true;
    }

    public async Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities)
    {
        if (entities == null || !entities.Any())
            throw new ArgumentException("Entities collection cannot be null or empty.", nameof(entities));

        var notFoundEntities = new List<string>();
        var existingEntities = new List<TEntity>();
        foreach (var entity in entities)
        {
            var idProperty = GetIdentityKeyProperty();
            var idValue = idProperty?.GetValue(entity) ?? throw new InvalidOperationException("Id value cannot be null.");
            var entityInDb = await session.LoadAsync<TEntity>((TKey)idValue);
            if (entityInDb == null)
                notFoundEntities.Add(idValue.ToString()!);
            else
                existingEntities.Add(entityInDb);
        }
        if (notFoundEntities.Count != 0)
            throw new NotFoundException($"{typeof(TEntity).Name} not found for the following IDs: {string.Join(", ", notFoundEntities)}");
        session.Delete(existingEntities.ToArray());
        return await Task.FromResult(true);
    }
    public async Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    {
        return await session.Query<TEntity>().AnyAsync(filter, token: cancellationToken);
    }
    private async Task IncludePropertiesAsync(IEnumerable<TEntity> result, List<string> includeProperties)
    {
        foreach (var includeProperty in includeProperties)
        {
            var navigationalPropertyInfo = GetNavigationalPropertyInfo(includeProperty);
            var foreignKeyPropertyInfo = GetForeignKeyPropertyInfo(includeProperty);
            foreach (var item in result)
                await LoadNavigationalPropertyAsync(item, navigationalPropertyInfo, foreignKeyPropertyInfo);
        }
    }

    private static PropertyInfo GetNavigationalPropertyInfo(string includeProperty)
    {
        return Array.Find(typeof(TEntity).GetProperties(), x => x.Name.Equals(includeProperty, StringComparison.OrdinalIgnoreCase)
        || x.Name.Equals($"{includeProperty}s", StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Property '{includeProperty}' does not exist on type '{typeof(TEntity).Name}'");
    }

    private static PropertyInfo GetForeignKeyPropertyInfo(string includeProperty)
    {
        return Array.Find(typeof(TEntity).GetProperties(), x => x.Name.Contains(includeProperty, StringComparison.OrdinalIgnoreCase) && (x.Name.EndsWith("Id") || x.Name.EndsWith("Ids")))
               ?? throw new ArgumentException($"Property '{includeProperty}' does not exist on type '{typeof(TEntity).Name}'");
    }

    private async Task LoadNavigationalPropertyAsync(TEntity item, PropertyInfo navigationalPropertyInfo, PropertyInfo foreignKeyPropertyInfo)
    {
        if (typeof(IEnumerable).IsAssignableFrom(navigationalPropertyInfo.PropertyType) && navigationalPropertyInfo.PropertyType != typeof(string))
            await LoadManyNavigationalPropertyAsync(item, navigationalPropertyInfo, foreignKeyPropertyInfo);
        else
            await LoadSingleNavigationalPropertyAsync(item, navigationalPropertyInfo, foreignKeyPropertyInfo);
    }

    private async Task LoadManyNavigationalPropertyAsync(TEntity item, PropertyInfo navigationalPropertyInfo, PropertyInfo foreignKeyPropertyInfo)
    {
        var navigationalPropertyType = navigationalPropertyInfo.PropertyType.GetGenericArguments()[0];
        Console.WriteLine(navigationalPropertyType.ToString());
        var loadManyAsync = typeof(IQuerySession)
                            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                            .First(m => m.Name == "LoadManyAsync"
                                        && m.GetParameters().Length == 1
                                        && m.GetParameters()[0].ParameterType.IsArray
                                        && m.GetParameters()[0].ParameterType.GetElementType() == typeof(TKey))
                            .MakeGenericMethod(navigationalPropertyType);

        if (foreignKeyPropertyInfo.GetValue(item) is not IEnumerable<TKey> foreignKeys) throw new ArgumentNullException(navigationalPropertyType.Name, "Foreign keys cannot be null");
        var loadManyTask = loadManyAsync.Invoke(session, [foreignKeys.ToArray()]) ?? throw new InvalidOperationException("Failed to load navigational properties.");
        var taskType = loadManyTask.GetType();
        var resultProperty = taskType.GetProperty("Result") ?? throw new InvalidOperationException("Result property not found on the task.");
        await ((Task)loadManyTask).ConfigureAwait(false);

        var navigations = resultProperty.GetValue(loadManyTask);
        navigationalPropertyInfo.SetValue(item, navigations);
    }
    private async Task LoadSingleNavigationalPropertyAsync(TEntity item, PropertyInfo navigationalPropertyInfo, PropertyInfo foreignKeyPropertyInfo)
    {
        var navigationalPropertyType = navigationalPropertyInfo.PropertyType;
        var loadAsync = typeof(IQuerySession)
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .First(m => m.Name == "LoadAsync"
                                    && m.GetParameters().Length == 2
                                    && m.GetParameters()[0].ParameterType == typeof(TKey))
                        .MakeGenericMethod(navigationalPropertyType);
        var foreignKeyValue = foreignKeyPropertyInfo.GetValue(item) ?? throw new ArgumentNullException(nameof(foreignKeyPropertyInfo), "Foreign key cannot be null");
        var foreignKey = (TKey)foreignKeyValue;
        // if (EqualityComparer<TKey>.Default.Equals(foreignKey, default))
        //     return default;

        var loadTask = loadAsync.Invoke(session, [foreignKey, CancellationToken.None]) ?? throw new InvalidOperationException("Failed to load navigational properties.");
        if (loadTask is Task task)
        {
            var taskType = task.GetType();
            var resultProperty = taskType.GetProperty("Result") ?? throw new InvalidOperationException("Result property not found on the task.");
            await task.ConfigureAwait(false);
            var navigations = resultProperty.GetValue(loadTask);
            navigationalPropertyInfo.SetValue(item, navigations);
        }
    }


    private static IMartenQueryable<TEntity> Filter(IMartenQueryable<TEntity> query, Expression<Func<TEntity, bool>>? filter)
    {
        if (filter != null) query = query.Where(filter) as IMartenQueryable<TEntity> ?? throw new InvalidOperationException("Query result cannot be null");
        return query;
    }

    private IMartenQueryable<TEntity> Include(IMartenQueryable<TEntity> query, List<string> includeProperties, Dictionary<string, object> dictionaries)
    {

        foreach (var includeProperty in includeProperties)
        {
            var navigationPropertyInfo = GetNavigationalPropertyInfo(includeProperty);
            var foreignKeyPropertyInfo = GetForeignKeyPropertyInfo(includeProperty);
            query = IncludeHelper(query, navigationPropertyInfo, foreignKeyPropertyInfo, out var dictionary);
            if (dictionary != null) dictionaries.Add(includeProperty, dictionary);
        }
        return query;
    }

    private void PopulateNavigationProperties(IEnumerable<TEntity> results, Dictionary<string, object> dictionaries)
    {
        foreach (var dictionary in dictionaries)
        {
            var includeProperty = dictionary.Key;
            var dictionaryInstance = dictionary.Value;
            var navigationPropertyInfo = GetNavigationalPropertyInfo(includeProperty);
            var foreignKeyPropertyInfo = GetForeignKeyPropertyInfo(includeProperty);
            foreach (var item in results) RepositoryMartenAsnc<TDocument, TEntity, TKey>.SetNavigationProperty(item, dictionaryInstance, navigationPropertyInfo, foreignKeyPropertyInfo);
        }
    }

    private static void SetNavigationProperty(TEntity item, object dictionaryInstance, PropertyInfo navigationPropertyInfo, PropertyInfo foreignKeyPropertyInfo)
    {
        if (foreignKeyPropertyInfo.PropertyType.GetGenericTypeDefinition() != typeof(ICollection<>))
        {
            var foreignKeyValue = foreignKeyPropertyInfo.GetValue(item);
            if (dictionaryInstance is IDictionary dic && foreignKeyValue != null && dic.Contains(foreignKeyValue))
            {
                var navigation = dic[foreignKeyValue];
                navigationPropertyInfo.SetValue(item, navigation);
            }
        }
        else
        {
            if (dictionaryInstance is IDictionary dic && foreignKeyPropertyInfo.GetValue(item) is IEnumerable<TKey> foreignKeyValues)
            {
                var navigationListType = typeof(List<>).MakeGenericType(navigationPropertyInfo.PropertyType.GetGenericArguments()[0]);
                var navigations = (IList)(Activator.CreateInstance(navigationListType) ?? throw new InvalidOperationException("Failed to create instance of navigation list type."));
                var filteredNavigations = foreignKeyValues
                    .Where(foreignKeyValue => dic.Contains(foreignKeyValue))
                    .Select(foreignKeyValue => dic[foreignKeyValue])
                    .ToList();
                foreach (var nav in filteredNavigations) navigations.Add(nav);
                navigationPropertyInfo.SetValue(item, navigations);
            }
        }
    }

    private static IMartenQueryable<TEntity> IncludeHelper(
       IMartenQueryable<TEntity> query, PropertyInfo navigationPropertyInfo,
       PropertyInfo foreignKeyPropertyInfo, out object? dictionaryInstance)
    {
        var keyType = foreignKeyPropertyInfo.PropertyType.IsGenericType &&
                      foreignKeyPropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)
            ? foreignKeyPropertyInfo.PropertyType.GetGenericArguments()[0]
            : foreignKeyPropertyInfo.PropertyType;

        var valueType = navigationPropertyInfo.PropertyType.IsGenericType &&
                        navigationPropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)
            ? navigationPropertyInfo.PropertyType.GetGenericArguments()[0]
            : navigationPropertyInfo.PropertyType;

        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        dictionaryInstance = Activator.CreateInstance(dictionaryType);

        var includeMethod = typeof(IMartenQueryable<TEntity>)
            .GetMethods()
            .First(m => m.Name == "Include" &&
                        m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType.IsGenericType &&
                        m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                        m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            .MakeGenericMethod(valueType, keyType);
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var foreignKeyAccess = Expression.Property(parameter, foreignKeyPropertyInfo);
        var convertedAccess = Expression.Convert(foreignKeyAccess, typeof(object));
        var idSourceExpression = Expression.Lambda<Func<TEntity, object>>(convertedAccess, parameter);
        var includeQuery = includeMethod.Invoke(query, new[] { idSourceExpression, dictionaryInstance });
        return includeQuery as IMartenQueryable<TEntity> ?? throw new InvalidOperationException("Include query cannot be null");
    }

    private PropertyInfo? GetIdentityKeyProperty()
    {
        return typeof(TEntity).GetProperties()
                 .FirstOrDefault(p => p.GetCustomAttributes(typeof(Marten.Schema.IdentityAttribute), true).Length != 0)
                 ?? typeof(TEntity).GetProperty("Id") ?? throw new InvalidOperationException("Identity property not found on entity.");
    }
}

