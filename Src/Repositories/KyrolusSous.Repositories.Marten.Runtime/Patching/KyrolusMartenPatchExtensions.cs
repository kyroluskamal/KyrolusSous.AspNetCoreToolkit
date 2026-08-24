using System.Linq.Expressions;
using Marten;
using Marten.Patching;

namespace KyrolusSous.Repositories.Marten.Runtime.Patching;

/// <summary>
/// Provides in-database JSON patching extensions directly on <see cref="IDocumentSession"/>.
/// Updates PostgreSQL JSONB structures without loading documents into memory.
/// </summary>
public static class KyrolusMartenPatchExtensions
{
    /// <summary>
    /// Atomically patches a document property using jsonb_set in PostgreSQL.
    /// </summary>
    public static async Task PatchSetAsync<TDoc, TKey, TProp>(
        this IDocumentSession session,
        TKey id,
        Expression<Func<TDoc, TProp>> propertyExpression,
        TProp value,
        CancellationToken cancellationToken = default)
        where TDoc : class
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(propertyExpression);

        if (id is Guid guidId)
        {
            session.Patch<TDoc>(guidId).Set(propertyExpression, value);
        }
        else if (id is string strId)
        {
            session.Patch<TDoc>(strId).Set(propertyExpression, value);
        }
        else if (id is int intId)
        {
            session.Patch<TDoc>(intId).Set(propertyExpression, value);
        }
        else if (id is long longId)
        {
            session.Patch<TDoc>(longId).Set(propertyExpression, value);
        }
        else
        {
            session.Patch<TDoc>(id.ToString()!).Set(propertyExpression, value);
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically increments a numeric property inside PostgreSQL.
    /// </summary>
    public static async Task PatchIncrementAsync<TDoc, TKey>(
        this IDocumentSession session,
        TKey id,
        Expression<Func<TDoc, int>> propertyExpression,
        int amount = 1,
        CancellationToken cancellationToken = default)
        where TDoc : class
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(propertyExpression);

        if (id is Guid guidId)
        {
            session.Patch<TDoc>(guidId).Increment(propertyExpression, amount);
        }
        else if (id is string strId)
        {
            session.Patch<TDoc>(strId).Increment(propertyExpression, amount);
        }
        else if (id is int intId)
        {
            session.Patch<TDoc>(intId).Increment(propertyExpression, amount);
        }
        else if (id is long longId)
        {
            session.Patch<TDoc>(longId).Increment(propertyExpression, amount);
        }
        else
        {
            session.Patch<TDoc>(id.ToString()!).Increment(propertyExpression, amount);
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically appends an element to a JSON array property in PostgreSQL.
    /// </summary>
    public static async Task PatchAppendElementAsync<TDoc, TKey, TElement>(
        this IDocumentSession session,
        TKey id,
        Expression<Func<TDoc, IEnumerable<TElement>>> listExpression,
        TElement element,
        CancellationToken cancellationToken = default)
        where TDoc : class
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(listExpression);

        if (id is Guid guidId)
        {
            session.Patch<TDoc>(guidId).Append(listExpression, element);
        }
        else if (id is string strId)
        {
            session.Patch<TDoc>(strId).Append(listExpression, element);
        }
        else if (id is int intId)
        {
            session.Patch<TDoc>(intId).Append(listExpression, element);
        }
        else if (id is long longId)
        {
            session.Patch<TDoc>(longId).Append(listExpression, element);
        }
        else
        {
            session.Patch<TDoc>(id.ToString()!).Append(listExpression, element);
        }

        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
