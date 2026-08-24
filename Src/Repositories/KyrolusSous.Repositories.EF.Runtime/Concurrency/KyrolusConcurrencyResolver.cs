using KyrolusSous.Repositories.EF.Abstractions.Concurrency;

namespace KyrolusSous.Repositories.EF.Runtime.Concurrency;

/// <summary>
/// Implements automatic optimistic concurrency resolution strategies.
/// </summary>
public sealed class KyrolusConcurrencyResolver : IKyrolusConcurrencyResolver
{
    public async Task<bool> ResolveConcurrencyConflictAsync(
        DbContext context,
        DbUpdateConcurrencyException exception,
        KyrolusConcurrencyStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exception);

        if (strategy == KyrolusConcurrencyStrategy.ThrowException)
        {
            return false;
        }

        foreach (var entry in exception.Entries)
        {
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
            if (databaseValues is null)
            {
                // Entity was deleted by another transaction
                entry.State = EntityState.Detached;
                continue;
            }

            switch (strategy)
            {
                case KyrolusConcurrencyStrategy.ClientWins:
                    // Client overwrites database values
                    entry.OriginalValues.SetValues(databaseValues);
                    break;

                case KyrolusConcurrencyStrategy.DatabaseWins:
                    // Discard client values and accept database values
                    entry.CurrentValues.SetValues(databaseValues);
                    entry.OriginalValues.SetValues(databaseValues);
                    entry.State = EntityState.Unchanged;
                    break;

                case KyrolusConcurrencyStrategy.Merge:
                    // Keep client modifications for modified properties; refresh others from database
                    var currentValues = entry.CurrentValues.Clone();
                    entry.OriginalValues.SetValues(databaseValues);

                    foreach (var property in entry.Properties)
                    {
                        if (property.IsModified)
                        {
                            property.CurrentValue = currentValues[property.Metadata.Name];
                        }
                    }
                    break;
            }
        }

        return true;
    }
}
