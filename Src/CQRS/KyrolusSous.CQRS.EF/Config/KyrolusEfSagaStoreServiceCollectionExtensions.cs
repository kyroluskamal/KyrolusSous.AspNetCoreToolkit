using KyrolusSous.CQRS.EF.Saga;
using KyrolusSous.CQRS.Saga;

namespace KyrolusSous.CQRS.EF.Config;

/// <summary>
/// Registration entry point wiring <see cref="EfSagaStore{TDbContext}"/> in as the application's
/// <see cref="IKyrolusSagaStore"/>.
/// </summary>
public static class KyrolusEfSagaStoreServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default in-memory saga store (see <c>AddKyrolusCqrsSaga</c>) with an EF Core-backed
    /// one persisted through <typeparamref name="TDbContext"/> - use this for any deployment where a
    /// saga must survive a process restart.
    /// </summary>
    /// <remarks>
    /// Two things this call does NOT do, both left to the consuming application because this package
    /// does not own <typeparamref name="TDbContext"/> or its model:
    /// <list type="bullet">
    /// <item><description>Register <typeparamref name="TDbContext"/> itself - call
    /// <c>services.AddDbContext&lt;TDbContext&gt;(...)</c> (or equivalent) separately, before or after
    /// this call, in either order.</description></item>
    /// <item><description>Map <see cref="KyrolusSagaInstanceEntity"/> into
    /// <typeparamref name="TDbContext"/>'s model - call
    /// <see cref="KyrolusSagaModelBuilderExtensions.ApplyKyrolusSagaStore"/> from
    /// <typeparamref name="TDbContext"/>'s own <c>OnModelCreating</c>.</description></item>
    /// </list>
    /// </remarks>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddKyrolusEfSagaStore<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKyrolusSagaStore<EfSagaStore<TDbContext>>();
        return services;
    }
}
