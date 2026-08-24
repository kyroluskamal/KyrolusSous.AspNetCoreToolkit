namespace KyrolusSous.Repositories.Marten.Abstractions.Includes;

/// <summary>
/// Fluent Include Graph builder for pre-populating associated documents in Marten query sessions.
/// </summary>
public sealed class KyrolusMartenIncludeGraph<TDoc>
    where TDoc : class
{
    private readonly List<Action<IMartenQueryable<TDoc>>> includeActions = [];

    /// <summary>
    /// Adds an include configuration for the document query.
    /// </summary>
    public KyrolusMartenIncludeGraph<TDoc> Include(Action<IMartenQueryable<TDoc>> includeAction)
    {
        ArgumentNullException.ThrowIfNull(includeAction);
        includeActions.Add(includeAction);
        return this;
    }

    /// <summary>
    /// Applies all configured includes to the Marten queryable.
    /// </summary>
    public IMartenQueryable<TDoc> Apply(IMartenQueryable<TDoc> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        foreach (var action in includeActions)
        {
            action(query);
        }

        return query;
    }
}
