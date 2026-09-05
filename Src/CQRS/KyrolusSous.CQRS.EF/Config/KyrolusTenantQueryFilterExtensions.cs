using Microsoft.EntityFrameworkCore.Metadata;

namespace KyrolusSous.CQRS.EF.Config;

/// <summary>
/// <see cref="ModelBuilder"/> extension that applies an automatic, per-tenant global query filter to
/// every entity type opted into it via <see cref="IKyrolusTenantOwnedEntity"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purely opt-in.</b> Nothing here runs unless the consuming application calls
/// <see cref="ApplyKyrolusTenantQueryFilters"/> itself from its own <c>OnModelCreating</c>, and even
/// then only entity types that implement <see cref="IKyrolusTenantOwnedEntity"/> are touched. An entity
/// with a <c>TenantId</c> property that does not implement the marker - the common case for every
/// existing consumer of this library today - is completely unaffected. This library does not own any
/// <c>DbContext</c>, so it cannot and does not apply this anywhere by itself; see
/// <see cref="KyrolusSous.CQRS.Abstractions.Behaviors.KyrolusTenantScopingBehavior{TRequest, TResponse}"/>
/// for the (also opt-in, but request-level rather than query-level) guard this package applies
/// automatically once registered.
/// </para>
/// <para>
/// <b>Why this takes the calling <see cref="DbContext"/> instance, not just a <see cref="Func{TResult}"/>.</b>
/// <c>OnModelCreating</c> runs exactly ONCE per <c>DbContext</c> type (EF Core caches the compiled model,
/// including every query filter expression), yet the whole point of this feature is that each request's
/// <c>DbContext</c> instance must see only ITS OWN caller's tenant. EF Core has exactly one supported
/// mechanism for a model-level query filter to depend on state that varies per instance: a filter that
/// reads data off <c>this</c> (the <c>DbContext</c> executing <c>OnModelCreating</c>) gets specially
/// rebound, on every query, to whichever <c>DbContext</c> instance is actually running that query - see
/// "Global Query Filters" in the EF Core docs, "Using DbContext instance data in query filters" - and
/// nothing else does. A closure captured any other way (a field on some other object, a raw
/// <c>Func&lt;string?&gt;</c> invoked directly, an <c>AsyncLocal&lt;T&gt;</c> read through a helper class)
/// is evaluated ONCE, the first time a query against the entity is compiled, and then silently reused -
/// frozen to whichever tenant happened to be current at that moment - for every later query from every
/// later caller and every later request, for the lifetime of the process. That failure mode is exactly
/// the "opt-in false sense of security" this feature exists to avoid, and it is invisible in casual
/// testing: the very first query against an entity is always correct, because that is the one that
/// compiles the filter. This method takes <c>dbContext</c> specifically so it can embed a
/// reference to it in the generated filter and ride EF's own per-instance rebinding, so
/// <c>currentTenantAccessor</c> is genuinely re-invoked on every query, from a scoped
/// <c>DbContext</c> or otherwise.
/// </para>
/// <para>
/// <b>Fails closed.</b> The filter this method builds is equivalent to
/// <c>e =&gt; !string.IsNullOrEmpty(currentTenantAccessor()) &amp;&amp; e.TenantId == currentTenantAccessor()</c>,
/// not the more obvious <c>e =&gt; e.TenantId == currentTenantAccessor()</c>. The difference matters: if
/// <c>currentTenantAccessor</c> ever returns <see langword="null"/> or <see cref="string.Empty"/>
/// - the accessor not wired up correctly, an unauthenticated background job, a bug in whatever resolves
/// the ambient tenant - the naive filter degrades into <c>e.TenantId == null</c>, which is still a
/// query filter but a dangerous one: it does not throw, does not return every tenant's rows either, but
/// it DOES return every row whose own <c>TenantId</c> happens to be null (legacy data, a seeding bug, a
/// forgotten column). That is an easy way to convince yourself isolation is enforced when it silently
/// is not for exactly the rows most likely to be unowned or misclassified. This method instead makes a
/// misconfigured accessor return zero rows for every tenant-owned entity, every time, with no exceptions
/// for entities whose own <c>TenantId</c> is null - "no ambient tenant" means "no data", full stop.
/// </para>
/// <para>Usage, from the consuming application's own <c>DbContext</c>:</para>
/// <code>
/// public class Blog : IKyrolusTenantOwnedEntity
/// {
///     public Guid Id { get; set; }
///     public string? TenantId { get; set; }
/// }
///
/// public class AppDbContext(DbContextOptions&lt;AppDbContext&gt; options, IKyrolusCurrentUserContext currentUser)
///     : DbContext(options)
/// {
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         modelBuilder.ApplyKyrolusTenantQueryFilters(this, () =&gt; currentUser.TenantId);
///     }
/// }
/// </code>
/// <para>
/// <c>currentTenantAccessor</c> must still read from something re-resolved on every call - a
/// captured, scoped <see cref="KyrolusSous.CQRS.Abstractions.Security.IKyrolusCurrentUserContext"/> (as
/// above), an <see cref="System.Threading.AsyncLocal{T}"/>, or similar - never a value captured once at
/// startup. The <c>dbContext</c> parameter is what makes that re-resolution actually happen
/// on every query instead of only the first; it does not, by itself, make a non-ambient accessor dynamic.
/// </para>
/// </remarks>
public static class KyrolusTenantQueryFilterExtensions
{
    /// <summary>
    /// The named query filter key this method registers each entity's filter under (see
    /// <see cref="Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.HasQueryFilter(string, System.Linq.Expressions.LambdaExpression)"/>).
    /// Using a dedicated key rather than the single unkeyed filter means this does not clobber a
    /// soft-delete filter, or any other filter, the consuming application already applied to the same
    /// entity type - EF Core ANDs every keyed filter together automatically.
    /// </summary>
    public const string FilterKey = "KyrolusTenantScope";

    /// <summary>
    /// Applies the fail-closed, per-tenant global query filter described in the type-level remarks to
    /// every entity type in <paramref name="modelBuilder"/>'s model that implements
    /// <see cref="IKyrolusTenantOwnedEntity"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder from the consuming application's own <c>OnModelCreating</c>.</param>
    /// <param name="dbContext">
    /// The <c>DbContext</c> instance <c>OnModelCreating</c> is running on - pass <c>this</c>. See the
    /// type-level remarks for why this is required: it is what lets EF Core rebind the filter to
    /// whichever <c>DbContext</c> instance is actually executing each query, rather than freezing to
    /// whichever instance happened to trigger the one-time model build.
    /// </param>
    /// <param name="currentTenantAccessor">
    /// Resolves the current ambient tenant id on every query execution. Must return <see langword="null"/>
    /// or an empty string when there is no usable current tenant - see the fail-closed remarks above for
    /// why that case matters as much as the happy path.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/>, for chaining.</returns>
    public static ModelBuilder ApplyKyrolusTenantQueryFilters(
        this ModelBuilder modelBuilder,
        DbContext dbContext,
        Func<string?> currentTenantAccessor)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(currentTenantAccessor);

        var isNullOrEmptyMethod = typeof(string).GetMethod(nameof(string.IsNullOrEmpty), [typeof(string)])!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var clrType = entityType.ClrType;
            if (!typeof(IKyrolusTenantOwnedEntity).IsAssignableFrom(clrType))
            {
                continue;
            }

            var filter = BuildTenantFilter(clrType, dbContext, currentTenantAccessor, isNullOrEmptyMethod);
            modelBuilder.Entity(clrType).HasQueryFilter(FilterKey, filter);
        }

        return modelBuilder;
    }

    private static LambdaExpression BuildTenantFilter(
        Type clrType,
        DbContext dbContext,
        Func<string?> currentTenantAccessor,
        MethodInfo isNullOrEmptyMethod)
    {
        var parameter = Expression.Parameter(clrType, "e");

        // Prefer the concrete class's own "TenantId" property (what EF actually maps to a column) over
        // the interface's, so the filter reads as a normal entity property access rather than an
        // interface cast - both work, but this is what EF's relational translators are most reliably
        // exercised against.
        var tenantIdProperty = clrType.GetProperty(nameof(IKyrolusTenantOwnedEntity.TenantId), BindingFlags.Public | BindingFlags.Instance);

        Expression tenantIdAccess = tenantIdProperty is not null
            ? Expression.Property(parameter, tenantIdProperty)
            : Expression.Property(Expression.Convert(parameter, typeof(IKyrolusTenantOwnedEntity)), nameof(IKyrolusTenantOwnedEntity.TenantId));

        // "resolve" ignores its own argument - its only job is to give the Invoke expression below a
        // DbContext-typed argument. EF Core specifically rebinds a constant of the model's own DbContext
        // type, everywhere it appears in a query filter, to whichever instance is actually running the
        // current query (see the type-level remarks). Because that rebound constant is not something EF
        // can fold to a fixed value ahead of time, the WHOLE InvocationExpression containing it - not just
        // the constant itself - is no longer eligible for the one-time partial evaluation that would
        // otherwise freeze this filter to a single tenant forever, so "resolve" genuinely runs on every
        // query execution, correctly re-invoking currentTenantAccessor each time. Dropping the dbContext
        // argument (invoking currentTenantAccessor directly) looks identical in a quick manual test - the
        // first query against any entity is always correct, since that is the one that compiles the
        // filter - and then silently stops working for every query after.
        Func<DbContext, string?> resolve = _ => currentTenantAccessor();
        var dbContextConstant = Expression.Constant(dbContext, dbContext.GetType());
        var resolveConstant = Expression.Constant(resolve, typeof(Func<DbContext, string?>));
        var currentTenant = Expression.Invoke(resolveConstant, dbContextConstant);

        var accessorHasValue = Expression.Not(Expression.Call(isNullOrEmptyMethod, currentTenant));
        var tenantMatches = Expression.Equal(tenantIdAccess, currentTenant);

        var body = Expression.AndAlso(accessorHasValue, tenantMatches);
        return Expression.Lambda(body, parameter);
    }
}
