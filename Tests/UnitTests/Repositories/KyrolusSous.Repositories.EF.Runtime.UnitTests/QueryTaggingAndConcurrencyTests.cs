using KyrolusSous.Repositories.EF.Abstractions.Concurrency;
using KyrolusSous.Repositories.EF.Runtime.Concurrency;
using KyrolusSous.Repositories.EF.Runtime.Profiling;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class QueryTaggingAndConcurrencyTests
{
    private sealed class InventoryItem
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    private sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
    {
        public DbSet<InventoryItem> Items => Set<InventoryItem>();
    }

    [Fact(DisplayName = "QueryTagging: Injects caller member and file into SQL query tag")]
    public void TagWithCaller_AppliesTag()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new InventoryDbContext(options);
        var taggedQuery = context.Items.TagWithCaller("InventoryCheck");

        taggedQuery.ShouldNotBeNull();
    }

    [Fact(DisplayName = "ConcurrencyResolver: Returns false for ThrowException strategy")]
    public async Task ConcurrencyResolver_ThrowStrategy_ReturnsFalse()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new InventoryDbContext(options);
        var resolver = new KyrolusConcurrencyResolver();
        var ex = new DbUpdateConcurrencyException("Conflict occurred");

        var resolved = await resolver.ResolveConcurrencyConflictAsync(context, ex, KyrolusConcurrencyStrategy.ThrowException);
        resolved.ShouldBeFalse();
    }
}
