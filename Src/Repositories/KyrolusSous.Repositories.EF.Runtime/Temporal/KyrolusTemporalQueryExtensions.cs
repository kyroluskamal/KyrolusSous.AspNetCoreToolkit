using System.Reflection;

namespace KyrolusSous.Repositories.EF.Runtime.Temporal;

/// <summary>
/// Provides temporal table LINQ query extensions over <see cref="IQueryable{TEntity}"/>.
/// </summary>
public static class KyrolusTemporalQueryExtensions
{
    private static MethodInfo? _temporalAsOfMethod;
    private static MethodInfo? _temporalBetweenMethod;
    private static MethodInfo? _temporalContainedInMethod;
    private static MethodInfo? _temporalAllMethod;
    private static bool _initialized;
    private static readonly object _lock = new();

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            var sqlServerAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Microsoft.EntityFrameworkCore.SqlServer");

            var extensionsType = sqlServerAssembly?.GetType("Microsoft.EntityFrameworkCore.SqlServerDbSetExtensions");
            if (extensionsType is not null)
            {
                var methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                _temporalAsOfMethod = methods.FirstOrDefault(m => m.Name == "TemporalAsOf");
                _temporalBetweenMethod = methods.FirstOrDefault(m => m.Name == "TemporalBetween");
                _temporalContainedInMethod = methods.FirstOrDefault(m => m.Name == "TemporalContainedIn");
                _temporalAllMethod = methods.FirstOrDefault(m => m.Name == "TemporalAll");
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Applies the EF Core <c>TemporalAsOf</c> time-travel operator when targeting system-versioned tables.
    /// </summary>
    public static IQueryable<TEntity> AsOf<TEntity>(this DbSet<TEntity> source, DateTime utcPointInTime)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureInitialized();

        if (_temporalAsOfMethod is not null)
        {
            var genericMethod = _temporalAsOfMethod.MakeGenericMethod(typeof(TEntity));
            return (IQueryable<TEntity>)genericMethod.Invoke(null, [source, utcPointInTime])!;
        }

        return source;
    }

    /// <summary>
    /// Applies the EF Core <c>TemporalBetween</c> operator when targeting system-versioned tables.
    /// </summary>
    public static IQueryable<TEntity> Between<TEntity>(this DbSet<TEntity> source, DateTime utcFrom, DateTime utcTo)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureInitialized();

        if (_temporalBetweenMethod is not null)
        {
            var genericMethod = _temporalBetweenMethod.MakeGenericMethod(typeof(TEntity));
            return (IQueryable<TEntity>)genericMethod.Invoke(null, [source, utcFrom, utcTo])!;
        }

        return source;
    }

    /// <summary>
    /// Applies the EF Core <c>TemporalContainedIn</c> operator when targeting system-versioned tables.
    /// </summary>
    public static IQueryable<TEntity> ContainedIn<TEntity>(this DbSet<TEntity> source, DateTime utcFrom, DateTime utcTo)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureInitialized();

        if (_temporalContainedInMethod is not null)
        {
            var genericMethod = _temporalContainedInMethod.MakeGenericMethod(typeof(TEntity));
            return (IQueryable<TEntity>)genericMethod.Invoke(null, [source, utcFrom, utcTo])!;
        }

        return source;
    }

    /// <summary>
    /// Applies the EF Core <c>TemporalAll</c> operator to retrieve all historical versions.
    /// </summary>
    public static IQueryable<TEntity> AllVersions<TEntity>(this DbSet<TEntity> source)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureInitialized();

        if (_temporalAllMethod is not null)
        {
            var genericMethod = _temporalAllMethod.MakeGenericMethod(typeof(TEntity));
            return (IQueryable<TEntity>)genericMethod.Invoke(null, [source])!;
        }

        return source;
    }
}
