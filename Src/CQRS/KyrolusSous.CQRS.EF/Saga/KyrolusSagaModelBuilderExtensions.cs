namespace KyrolusSous.CQRS.EF.Saga;

/// <summary>
/// <see cref="ModelBuilder"/> extension that maps <see cref="KyrolusSagaInstanceEntity"/> - the table
/// <see cref="EfSagaStore{TDbContext}"/> reads and writes - into the consuming application's own
/// <c>DbContext</c> model.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purely opt-in</b>, mirroring
/// <see cref="KyrolusSous.CQRS.EF.Config.KyrolusTenantQueryFilterExtensions.ApplyKyrolusTenantQueryFilters"/>:
/// this package does not own any <c>DbContext</c>, so nothing here runs unless the consuming
/// application calls <see cref="ApplyKyrolusSagaStore"/> itself from its own <c>OnModelCreating</c> -
/// which, like every <c>OnModelCreating</c> override, only ever runs once per <c>DbContext</c> type, so
/// there is no per-instance state to worry about here the way
/// <see cref="KyrolusSous.CQRS.EF.Config.KyrolusTenantQueryFilterExtensions"/>'s query filter has to.
/// </para>
/// <para>Usage, from the consuming application's own <c>DbContext</c>:</para>
/// <code>
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     base.OnModelCreating(modelBuilder);
///     modelBuilder.ApplyKyrolusSagaStore();
/// }
/// </code>
/// </remarks>
public static class KyrolusSagaModelBuilderExtensions
{
    /// <summary>The table name <see cref="KyrolusSagaInstanceEntity"/> maps to unless overridden.</summary>
    public const string DefaultTableName = "KyrolusSagaInstances";

    /// <summary>
    /// Maps <see cref="KyrolusSagaInstanceEntity"/> into <paramref name="modelBuilder"/>'s model:
    /// <see cref="KyrolusSagaInstanceEntity.Id"/> as the primary key, plus an index on
    /// <see cref="KyrolusSagaInstanceEntity.CorrelationId"/> (what
    /// <see cref="EfSagaStore{TDbContext}.GetByCorrelationIdAsync"/> queries by) and one on
    /// <see cref="KyrolusSagaInstanceEntity.Status"/> (what
    /// <see cref="EfSagaStore{TDbContext}.GetIncompleteAsync"/> filters by).
    /// </summary>
    /// <param name="modelBuilder">The model builder from the consuming application's own <c>OnModelCreating</c>.</param>
    /// <param name="tableName">Overrides <see cref="DefaultTableName"/>, for an application that needs a different table name.</param>
    /// <returns>The same <paramref name="modelBuilder"/>, for chaining.</returns>
    public static ModelBuilder ApplyKyrolusSagaStore(this ModelBuilder modelBuilder, string tableName = DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        modelBuilder.Entity<KyrolusSagaInstanceEntity>(b =>
        {
            b.ToTable(tableName);
            b.HasKey(e => e.Id);
            b.Property(e => e.SagaName).IsRequired();
            b.Property(e => e.ContextJson).IsRequired();
            b.HasIndex(e => e.CorrelationId);
            b.HasIndex(e => e.Status);
        });

        return modelBuilder;
    }
}
