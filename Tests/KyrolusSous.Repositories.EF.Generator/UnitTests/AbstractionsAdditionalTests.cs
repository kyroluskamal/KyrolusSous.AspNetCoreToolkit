using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Shouldly;
using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class AbstractionsAdditionalTests
{
    private sealed class NoRowVersionEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NoRowVersionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<NoRowVersionEntity> Items => Set<NoRowVersionEntity>();
    }

    private sealed class FakeConcurrencyException(IReadOnlyList<EntityEntry> entries) : DbUpdateConcurrencyException("fake")
    {
        private readonly IReadOnlyList<EntityEntry> entries = entries;

        public override IReadOnlyList<EntityEntry> Entries => entries;
    }
#pragma warning disable S1144
    private sealed class GraphDummy
    {
        public int Id { get; set; }
        public GraphChild? Child { get; set; } = default!;
    }

    private sealed class GraphChild
    {
        public string? Name { get; set; } = default!;
    }
#pragma warning restore S1144
    [Fact(DisplayName = "BuildConcurrencyInfoAsync handles missing row-version property gracefully")]
    public async Task BuildConcurrencyInfoAsync_MissingRowVersion_ReturnsDatabaseValues()
    {
        var options = new DbContextOptionsBuilder<NoRowVersionContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using (var setup = new NoRowVersionContext(options))
        {
            setup.Items.Add(new NoRowVersionEntity { Id = 7, Name = "n1" });
            await setup.SaveChangesAsync();
        }

        using var ctx = new NoRowVersionContext(options);
        var entry = ctx.Entry(await ctx.Items.FirstAsync());
        var ex = new FakeConcurrencyException([entry]);

        var info = await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, "DoesNotExist");

        info.ShouldNotBeNull();
        info!.Value.DatabaseValues.ShouldNotBeNull();
        info.Value.CurrentRowVersion.ShouldBeNull();
        info.Value.OriginalRowVersion.ShouldBeNull();
        info.Value.DatabaseValues!["Id"].ShouldBe(7);
    }

    [Fact(DisplayName = "BuildConcurrencyInfoAsync works when row-version name is null")]
    public async Task BuildConcurrencyInfoAsync_NullRowVersionName_ReturnsValues()
    {
        var options = new DbContextOptionsBuilder<NoRowVersionContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using (var setup = new NoRowVersionContext(options))
        {
            setup.Items.Add(new NoRowVersionEntity { Id = 3, Name = "n3" });
            await setup.SaveChangesAsync();
        }

        using var ctx = new NoRowVersionContext(options);
        var entry = ctx.Entry(await ctx.Items.FirstAsync());
        var ex = new FakeConcurrencyException([entry]);

        var info = await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, null);

        info.ShouldNotBeNull();
        info!.Value.DatabaseValues.ShouldNotBeNull();
        info.Value.DatabaseValues!["Name"].ShouldBe("n3");
        info.Value.CurrentRowVersion.ShouldBeNull();
        info.Value.OriginalRowVersion.ShouldBeNull();
    }

    [Fact(DisplayName = "IncludeGraph stores provided expressions")]
    public void IncludeGraph_StoresIncludes()
    {
        Expression<Func<GraphDummy, object?>> first = e => e.Child;
        Expression<Func<GraphDummy, object?>> second = e => e.Child!.Name!;
        var graph = new IncludeGraph<GraphDummy>(first, second);
        graph.Includes.Count.ShouldBe(2);
        graph.Includes[0].ShouldBe(first);
        graph.Includes[1].ShouldBe(second);
    }

    [Fact(DisplayName = "IncludeGraphBuilder returns empty graph on null paths")]
    public void IncludeGraphBuilder_ReturnsEmptyOnNull()
    {
        string[]? paths = null;
        var graph = KyrolusIncludeGraphBuilder.FromPaths<GraphDummy>(paths);
        graph.Includes.ShouldNotBeNull();
        graph.Includes.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "RepositoryOperationResult factory methods set fields correctly")]
    public void RepositoryOperationResult_FactoryMethods()
    {
        var success = RepositoryOperationResult<int>.Success(9, pendingSave: true);
        success.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        success.Value.ShouldBe(9);
        success.PendingSave.ShouldBeTrue();

        var notFound = RepositoryOperationResult<string>.NotFound();
        notFound.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
        notFound.Value.ShouldBeNull();

        var conflictInfo = new ConcurrencyInfo(null, null, null, retryCount: 2);
        var conflict = RepositoryOperationResult<string>.ConcurrencyConflict(new InvalidOperationException("x"), conflictInfo);
        conflict.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
        conflict.Exception.ShouldBeOfType<InvalidOperationException>();
        conflict.Concurrency?.RetryCount.ShouldBe(2);

        var failed = RepositoryOperationResult<string>.Failed(new ApplicationException("bad"));
        failed.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        failed.Exception.ShouldBeOfType<ApplicationException>();

        var pending = RepositoryOperationResult<string>.Pending("val");
        pending.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        pending.PendingSave.ShouldBeTrue();
        pending.Value.ShouldBe("val");
    }
}
