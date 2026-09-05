using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.EF.Config;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusTenantQueryFilterTests
{
    // The filter must react to whichever tenant is "current" AT QUERY TIME, not at model-build time -
    // OnModelCreating only ever runs once per DbContext type, so the accessor closure it captures has to
    // keep reading fresh ambient state on every call rather than a value fixed when the model was built.
    // A single static delegate backed by an AsyncLocal is exactly that shape (and is also what the real
    // extension's XML doc tells callers to use - a scoped IKyrolusCurrentUserContext, an AsyncLocal, never
    // a value captured once at startup), so it is the most honest way to exercise this in a test too.
    // ApplyKyrolusTenantQueryFilters additionally requires the calling DbContext instance itself (passed
    // as "this" below) - see its XML doc for why: without it, EF Core cannot re-bind the filter to
    // whichever DbContext instance is actually running each query, and it would silently freeze to
    // whichever instance happened to trigger the one-time model build.
    private static readonly AsyncLocal<string?> CurrentTenant = new();
    private static readonly Func<string?> Accessor = () => CurrentTenant.Value;

    public sealed class TenantOwnedProduct : IKyrolusTenantOwnedEntity
    {
        public Guid Id { get; set; }
        public string? TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UntenantedProduct
    {
        public Guid Id { get; set; }
        public string? TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TenantOwnedProduct> TenantOwnedProducts => Set<TenantOwnedProduct>();
        public DbSet<UntenantedProduct> UntenantedProducts => Set<UntenantedProduct>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyKyrolusTenantQueryFilters(this, Accessor);
        }
    }

    // One DbContextOptions instance per logical "database" for the lifetime of a test, shared by every
    // TestDbContext built against it - the EF Core in-memory provider keys its data store off the options
    // instance (via its default InMemoryDatabaseRoot), not just off the database name string, so two
    // separately-built DbContextOptions pointed at "the same name" can end up reading/writing different
    // stores. Building the options once per test and reusing it for both the seeding context and the
    // querying context avoids that trap.
    private static DbContextOptions<TestDbContext> CreateOptions()
        => new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact(DisplayName = "TenantQueryFilter: opted-in entity only returns the current tenant's rows, never another tenant's")]
    public async Task TenantOwnedEntity_OnlyReturnsCurrentTenantRows()
    {
        var options = CreateOptions();

        // Query filters only affect reads, so the ambient tenant during seeding is irrelevant -
        // Add/SaveChanges is never subject to the filter.
        CurrentTenant.Value = null;
        await using (var seed = new TestDbContext(options))
        {
            seed.TenantOwnedProducts.AddRange(
                new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = "tenant-a", Name = "A1" },
                new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = "tenant-a", Name = "A2" },
                new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = "tenant-b", Name = "B1" });
            await seed.SaveChangesAsync();
        }

        CurrentTenant.Value = "tenant-a";
        await using var context = new TestDbContext(options);
        var rows = await context.TenantOwnedProducts.ToListAsync();

        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(p => p.TenantId == "tenant-a");
    }

    [Fact(DisplayName = "TenantQueryFilter: fails closed on a null accessor - zero rows, even ones whose own TenantId is null")]
    public async Task TenantOwnedEntity_NullAccessor_ReturnsZeroRows()
    {
        var options = CreateOptions();

        CurrentTenant.Value = null;
        await using (var seed = new TestDbContext(options))
        {
            seed.TenantOwnedProducts.AddRange(
                new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = "tenant-a", Name = "A1" },
                new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = "tenant-b", Name = "B1" },
                // Simulates legacy/seed data, or a bug elsewhere that never set TenantId. A naive
                // "e.TenantId == currentTenantAccessor()" filter would let this row through whenever the
                // accessor is also misconfigured to return null, since null == null. It must not.
                new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = null, Name = "NoTenant" });
            await seed.SaveChangesAsync();
        }

        CurrentTenant.Value = null;
        await using var context = new TestDbContext(options);
        var rows = await context.TenantOwnedProducts.ToListAsync();

        rows.ShouldBeEmpty();
    }

    [Fact(DisplayName = "TenantQueryFilter: fails closed on an empty-string accessor too, not just null")]
    public async Task TenantOwnedEntity_EmptyAccessor_ReturnsZeroRows()
    {
        var options = CreateOptions();

        CurrentTenant.Value = null;
        await using (var seed = new TestDbContext(options))
        {
            seed.TenantOwnedProducts.Add(new TenantOwnedProduct { Id = Guid.NewGuid(), TenantId = "tenant-a", Name = "A1" });
            await seed.SaveChangesAsync();
        }

        CurrentTenant.Value = string.Empty;
        await using var context = new TestDbContext(options);
        var rows = await context.TenantOwnedProducts.ToListAsync();

        rows.ShouldBeEmpty();
    }

    [Fact(DisplayName = "TenantQueryFilter: an entity that does not implement the marker interface is completely unaffected")]
    public async Task NonMarkerEntity_IsNeverFiltered()
    {
        var options = CreateOptions();

        CurrentTenant.Value = null;
        await using (var seed = new TestDbContext(options))
        {
            seed.UntenantedProducts.AddRange(
                new UntenantedProduct { Id = Guid.NewGuid(), TenantId = "tenant-a", Name = "A1" },
                new UntenantedProduct { Id = Guid.NewGuid(), TenantId = "tenant-b", Name = "B1" });
            await seed.SaveChangesAsync();
        }

        // Even with an ambient tenant that would fail every tenant-owned entity closed, a type that
        // never opted in via IKyrolusTenantOwnedEntity must behave exactly as if this feature did not
        // exist.
        CurrentTenant.Value = null;
        await using var context = new TestDbContext(options);
        var rows = await context.UntenantedProducts.ToListAsync();

        rows.Count.ShouldBe(2);
    }
}
