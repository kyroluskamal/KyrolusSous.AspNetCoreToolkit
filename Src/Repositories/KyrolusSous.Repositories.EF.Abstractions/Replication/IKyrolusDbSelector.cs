namespace KyrolusSous.Repositories.EF.Abstractions.Replication;

/// <summary>
/// Defines the target database role for replication routing.
/// </summary>
public enum KyrolusDatabaseRole
{
    /// <summary>
    /// The primary master database for read-write operations.
    /// </summary>
    Primary,

    /// <summary>
    /// A read-replica database optimized for read-only queries.
    /// </summary>
    ReadReplica
}

/// <summary>
/// Selects the appropriate <typeparamref name="TDbContext"/> instance based on read vs write operations.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type.</typeparam>
public interface IKyrolusDbSelector<out TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Gets a DbContext configured for the specified database role.
    /// </summary>
    TDbContext GetDbContext(KyrolusDatabaseRole role = KyrolusDatabaseRole.Primary);
}
