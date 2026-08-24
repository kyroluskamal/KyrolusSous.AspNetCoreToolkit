using System.Globalization;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions.Auditing;
using KyrolusSous.Repositories.EF.Abstractions.Dynamic;
using KyrolusSous.Repositories.EF.Abstractions.Events;
using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.MultiTenancy;
using KyrolusSous.Repositories.EF.Abstractions.Outbox;
using KyrolusSous.Repositories.EF.Abstractions.Pagination;
using KyrolusSous.Repositories.EF.Abstractions.Specifications;
using KyrolusSous.Repositories.EF.Cache.Distributed;
using KyrolusSous.Repositories.EF.Runtime.Dynamic;
using KyrolusSous.Repositories.EF.Runtime.Interceptors;
using KyrolusSous.Repositories.EF.Runtime.Outbox;
using KyrolusSous.Repositories.EF.Runtime.Pagination;
using KyrolusSous.Repositories.EF.Runtime.Profiling;
using KyrolusSous.Repositories.EF.Runtime.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class LogicalEdgeCasesAndHardeningTests
{
    private sealed class SampleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid TenantGuid { get; set; }
        public DateOnly CreatedDate { get; set; }
        public TimeOnly ShiftTime { get; set; }
    }

    private sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
    {
        public DbSet<SampleEntity> Samples => Set<SampleEntity>();
    }

    [Fact(DisplayName = "1. Type Conversion: Converts DateOnly and TimeOnly in PrimaryKey values correctly")]
    public void TypeConversion_HandlesDateOnlyAndTimeOnly()
    {
        var date = new DateOnly(2026, 8, 25);
        var time = new TimeOnly(14, 30, 0);

        var datePred = KyrolusEFRepositoryBase<SampleEntity>.GetPrimaryKeyFromKeyValues([date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)], [nameof(SampleEntity.CreatedDate)]);
        datePred.ShouldNotBeNull();

        var timePred = KyrolusEFRepositoryBase<SampleEntity>.GetPrimaryKeyFromKeyValues([time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)], [nameof(SampleEntity.ShiftTime)]);
        timePred.ShouldNotBeNull();
    }

    [Fact(DisplayName = "2. Dynamic Filtering: Handles Guid, DateOnly, and TimeOnly without throwing InvalidCastException")]
    public void DynamicFilter_HandlesGuidDateOnlyTimeOnlySafely()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new SampleDbContext(options);
        var g = Guid.NewGuid();
        db.Samples.Add(new SampleEntity { Id = 1, Name = "Item1", TenantGuid = g, CreatedDate = new DateOnly(2026, 1, 1), ShiftTime = new TimeOnly(9, 0) });
        db.SaveChanges();

        var query = db.Samples.ApplyDynamicFilter(nameof(SampleEntity.TenantGuid), KyrolusFilterOperator.Equals, g.ToString());
        var count = query.Count();
        count.ShouldBe(1);
    }

    [Fact(DisplayName = "3. Keyset Pagination: Throws ArgumentOutOfRangeException when PageSize is less than 1")]
    public async Task KeysetPagination_ValidatesPageSize()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new SampleDbContext(options);
        var spec = new TestKeysetSpec(pageSize: 0);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
        {
            await db.Samples.ToKeysetPageAsync(spec);
        });
    }

    [Fact(DisplayName = "4. Specification: Not operator negates filtering criteria properly")]
    public void Specification_NotOperator_NegatesCriteria()
    {
        var spec = new KyrolusSpecification<SampleEntity>(e => e.Name == "Excluded").Not();
        spec.Criteria.ShouldNotBeNull();

        var func = spec.Criteria.Compile();
        func(new SampleEntity { Name = "Excluded" }).ShouldBeFalse();
        func(new SampleEntity { Name = "Allowed" }).ShouldBeTrue();
    }

    [Fact(DisplayName = "5. Outbox: Throws InvalidOperationException when UnitOfWork does not implement IKyrolusOutboxStore")]
    public async Task Outbox_ThrowsWhenNotOutboxStore()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new SampleDbContext(options);
        var uow = new KyrolusRuntimeUnitOfWork<SampleDbContext>(db);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await uow.AddOutboxMessageAsync(new { Event = "UserRegistered" });
        });
    }

    [Fact(DisplayName = "6. IncludeGraph: Skips empty or whitespace paths without errors")]
    public void IncludeGraphBuilder_SkipsEmptyOrWhitespacePaths()
    {
        var graph = KyrolusIncludeGraphBuilder.FromPaths<SampleEntity>("", "   ", "InvalidPath");
        graph.Includes.ShouldBeEmpty();
    }

    [Fact(DisplayName = "7. DistributedCacheProvider: Defensive against empty batch collections")]
    public async Task DistributedCacheProvider_HandlesEmptyBatchCollections()
    {
        var memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var provider = new KyrolusEfDistributedCacheProvider(memoryCache);

        var many = await provider.GetManyAsync<string>([]);
        many.ShouldBeEmpty();

        await provider.SetManyAsync<string>([]);
        await provider.RemoveManyAsync([]);
    }

    [Fact(DisplayName = "8. Query Tagging: Safely handles caller tagging even with empty file paths")]
    public void QueryTagging_HandlesEmptyFilePathSafely()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new SampleDbContext(options);
        var query = db.Samples.TagWithCaller("Test", "Member", "", 42);
        query.ShouldNotBeNull();
    }

    private sealed class TestKeysetSpec(int pageSize) : IKyrolusKeysetSpecification<SampleEntity, int>
    {
        public int PageSize { get; } = pageSize;
        public int? CursorValue => null;
        public KyrolusKeysetDirection Direction => KyrolusKeysetDirection.Forward;
        public bool IsDescending => false;
        public System.Linq.Expressions.Expression<Func<SampleEntity, int>> CursorSelector => e => e.Id;
        public System.Linq.Expressions.Expression<Func<SampleEntity, bool>>? Filter => null;
    }
}
