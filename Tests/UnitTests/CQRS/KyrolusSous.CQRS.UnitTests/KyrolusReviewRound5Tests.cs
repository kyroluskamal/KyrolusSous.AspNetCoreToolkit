using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.EF.Command.Bulk;
using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Regression tests for the fifth review round, scoped to KyrolusSous.CQRS.EF: the Enum/Nullable
/// seek-cursor crashes in GetSeekQueryHandler, the IncludeProperties-dropping/IncludeGraph-duplicating
/// bug shared by GetAllQueryHandler/GetByIdQueryHandler/GetByKeyValuesQueryHandler, the missing
/// batch-size guard on BulkUpsertCommandHandler, and the missing always-on Key/Timestamp/
/// ConcurrencyCheck/Computed protection in ExecuteUpdateCommandHandler.
/// </summary>
public sealed class KyrolusReviewRound5Tests
{
    public sealed class DummyDbContext : DbContext;

    public enum StubStatus { Draft, Active, Archived }

    public sealed class EnumSeekEntity
    {
        public int Id { get; set; }
        public StubStatus Status { get; set; }
    }

    public sealed class NullableSeekEntity
    {
        public int Id { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class IncludeTestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }

    public sealed class BulkEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ProtectedPropsEntity
    {
        [Key]
        public int Id { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        [ConcurrencyCheck]
        public int Version { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string Computed { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    #region Bug1a EF: GetSeekQueryHandler must not throw building the cursor predicate for an Enum sort column

    [Fact(DisplayName = "Bug1a EF: GetSeekQueryHandler builds a page-2 cursor predicate for an Enum column without throwing")]
    public async Task GetSeekQueryHandler_EnumSortColumn_Page2_DoesNotThrow()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, EnumSeekEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, EnumSeekEntity, int>>().Returns(repo);

        var item = new EnumSeekEntity { Id = 1, Status = StubStatus.Active };
        repo.GetPagedAsync(Arg.Any<IKyrolusPagedQuerySpecification<EnumSeekEntity, EnumSeekEntity>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(IReadOnlyList<EnumSeekEntity> Items, int TotalCount)>(([item], 1)));

        var handler = new GetSeekQueryHandler<DummyDbContext, EnumSeekEntity, int>(uow);

        var page1Query = new GetSeekQuery<EnumSeekEntity, int>(pageSize: 1) { SeekPropertyNames = ["Status"] };
        var page1 = await handler.Handle(page1Query, CancellationToken.None);
        page1.NextToken.ShouldNotBeNull();

        var page2Query = new GetSeekQuery<EnumSeekEntity, int>(pageSize: 1, cursor: page1.NextToken) { SeekPropertyNames = ["Status"] };

        // Before the fix, GetMethod("CompareTo", [enumType]) resolved to the inherited
        // Enum.CompareTo(object), and Expression.Call then rejected the enum-typed (not
        // object-typed) argument built for it - ArgumentException at expression-build time.
        var page2 = await handler.Handle(page2Query, CancellationToken.None);

        page2.ShouldNotBeNull();
    }

    #endregion

    #region Bug1b EF: GetSeekQueryHandler must not throw building the cursor predicate for a null Nullable<T> cursor value

    [Fact(DisplayName = "Bug1b EF: GetSeekQueryHandler builds a page-2 cursor predicate for a null Nullable<T> cursor value without throwing")]
    public async Task GetSeekQueryHandler_NullableSortColumn_NullCursorValue_DoesNotThrow()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, NullableSeekEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, NullableSeekEntity, int>>().Returns(repo);

        var item = new NullableSeekEntity { Id = 1, ExpiresAt = null };
        repo.GetPagedAsync(Arg.Any<IKyrolusPagedQuerySpecification<NullableSeekEntity, NullableSeekEntity>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(IReadOnlyList<NullableSeekEntity> Items, int TotalCount)>(([item], 1)));

        var handler = new GetSeekQueryHandler<DummyDbContext, NullableSeekEntity, int>(uow);

        var page1Query = new GetSeekQuery<NullableSeekEntity, int>(pageSize: 1) { SeekPropertyNames = ["ExpiresAt"] };
        var page1 = await handler.Handle(page1Query, CancellationToken.None);
        page1.NextToken.ShouldNotBeNull();

        var page2Query = new GetSeekQuery<NullableSeekEntity, int>(pageSize: 1, cursor: page1.NextToken) { SeekPropertyNames = ["ExpiresAt"] };

        // Before the fix, the null branch built Expression.Constant(null, underlying) - e.g.
        // Expression.Constant(null, typeof(DateTime)) - which the CLR rejects for a non-nullable
        // value type: "Argument types do not match".
        var page2 = await handler.Handle(page2Query, CancellationToken.None);

        page2.ShouldNotBeNull();
    }

    #endregion

    #region Bug2 EF: IncludeProperties must not be dropped, and IncludeGraph must not be duplicated

    [Fact(DisplayName = "Bug2 EF: GetAllQueryHandler merges IncludeProperties with IncludeGraph instead of dropping IncludeProperties")]
    public async Task GetAllQueryHandler_IncludePropertiesPlusIncludeGraph_BothPresent()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>().Returns(repo);

        Expression<Func<IncludeTestEntity, object?>>[]? captured = null;
        repo.GetAllAsync(
                Arg.Any<Expression<Func<IncludeTestEntity, bool>>?>(),
                Arg.Any<Func<IQueryable<IncludeTestEntity>, IOrderedQueryable<IncludeTestEntity>>?>(),
                Arg.Any<bool?>(),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>(),
                Arg.Do<Expression<Func<IncludeTestEntity, object?>>[]>(arr => captured = arr))
            .Returns(Task.FromResult<IEnumerable<IncludeTestEntity>>([]));

        var handler = new GetAllQueryHandler<DummyDbContext, IncludeTestEntity, int>(uow);
        var query = new GetAllQuery<IncludeTestEntity>
        {
            IncludeProperties = ["Name"],
            IncludeGraph = new IncludeGraph<IncludeTestEntity>(x => x.Tag)
        };

        await handler.Handle(query, CancellationToken.None);

        // Before the fix this array held only the IncludeGraph entry (1) - IncludeProperties was
        // silently dropped whenever combined with IncludeGraph/IncludeExpressions.
        captured.ShouldNotBeNull();
        captured!.Length.ShouldBe(2);
    }

    [Fact(DisplayName = "Bug2 EF: GetAllQueryHandler's Selector path merges all three include sources without duplicating IncludeGraph")]
    public async Task GetAllQueryHandler_Selector_IncludePropertiesPlusIncludeGraph_NoDuplicate()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>().Returns(repo);

        Expression<Func<IncludeTestEntity, object?>>[]? captured = null;
        repo.QueryAsync(
                Arg.Do<IKyrolusQuerySpecification<IncludeTestEntity, IncludeTestEntity>>(spec => captured = spec.Includes),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<IncludeTestEntity>()));

        var handler = new GetAllQueryHandler<DummyDbContext, IncludeTestEntity, int>(uow);
        var query = new GetAllQuery<IncludeTestEntity>
        {
            IncludeProperties = ["Name"],
            IncludeGraph = new IncludeGraph<IncludeTestEntity>(x => x.Tag),
            Selector = x => x
        };

        await handler.Handle(query, CancellationToken.None);

        // Before the fix this held 3 entries: IncludeProperties(1) + IncludeGraph re-merged
        // twice (once directly, once nested inside the already-merged Graph+Expressions array).
        captured.ShouldNotBeNull();
        captured!.Length.ShouldBe(2);
    }

    [Fact(DisplayName = "Bug2 EF: GetByIdQueryHandler merges IncludeProperties with IncludeGraph instead of dropping IncludeProperties")]
    public async Task GetByIdQueryHandler_IncludePropertiesPlusIncludeGraph_BothPresent()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusSingleKeyRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>();
        uow.GetRepository<IKyrolusSingleKeyRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>().Returns(repo);

        Expression<Func<IncludeTestEntity, object?>>[]? captured = null;
        repo.GetByIdAsync(
                Arg.Any<int>(),
                Arg.Any<bool?>(),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>(),
                Arg.Do<Expression<Func<IncludeTestEntity, object?>>[]>(arr => captured = arr))
            .Returns(Task.FromResult<IncludeTestEntity?>(null));

        var handler = new GetByIdQueryHandler<DummyDbContext, IncludeTestEntity, int>(uow);
        var query = new GetByIdQuery<IncludeTestEntity, int>(1)
        {
            IncludeProperties = ["Name"],
            IncludeGraph = new IncludeGraph<IncludeTestEntity>(x => x.Tag)
        };

        await handler.Handle(query, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured!.Length.ShouldBe(2);
    }

    [Fact(DisplayName = "Bug2 EF: GetByKeyValuesQueryHandler merges IncludeProperties with IncludeGraph instead of dropping IncludeProperties")]
    public async Task GetByKeyValuesQueryHandler_IncludePropertiesPlusIncludeGraph_BothPresent()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusCompositeKeyRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>();
        uow.GetRepository<IKyrolusCompositeKeyRepositoryAsync<DummyDbContext, IncludeTestEntity, int>>().Returns(repo);

        Expression<Func<IncludeTestEntity, object?>>[]? captured = null;
        repo.GetByIdAsync(
                Arg.Any<object?[]>(),
                Arg.Any<bool?>(),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>(),
                Arg.Do<Expression<Func<IncludeTestEntity, object?>>[]>(arr => captured = arr))
            .Returns(Task.FromResult<IncludeTestEntity?>(null));

        var handler = new GetByKeyValuesQueryHandler<DummyDbContext, IncludeTestEntity, int>(uow);
        var query = new GetByKeyValuesQuery<IncludeTestEntity, int>([1])
        {
            IncludeProperties = ["Name"],
            IncludeGraph = new IncludeGraph<IncludeTestEntity>(x => x.Tag)
        };

        await handler.Handle(query, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured!.Length.ShouldBe(2);
    }

    #endregion

    #region Bug3 EF: BulkUpsertCommandHandler must cap batch size instead of building an unbounded existence-check query

    [Fact(DisplayName = "Bug3 EF: BulkUpsertCommandHandler rejects a batch larger than KyrolusBulkLimits.MaxBatchSize")]
    public async Task BulkUpsertCommandHandler_RejectsOversizedBatch()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var handler = new BulkUpsertCommandHandler<DummyDbContext, BulkEntity, int>(uow);
        var entities = Enumerable.Range(1, KyrolusBulkLimits.MaxBatchSize + 1)
            .Select(i => new BulkEntity { Id = i, Name = $"n{i}" })
            .ToList();
        var command = new BulkUpsertCommand<BulkEntity, int>(entities, ["Id"]);

        // Before the fix, nothing capped batch size here; the existence-check query builds one
        // OR-branch (with its own parameters) per entity, and SQL Server rejects a query with
        // more than ~2100 parameters.
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        ex.Message.ShouldContain(KyrolusBulkLimits.MaxBatchSize.ToString());
    }

    [Fact(DisplayName = "Bug3 EF: BulkUpsertCommandHandler allows a batch exactly at KyrolusBulkLimits.MaxBatchSize")]
    public async Task BulkUpsertCommandHandler_AllowsBatchAtMaxBatchSize()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, BulkEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, BulkEntity, int>>().Returns(repo);
        repo.GetAllAsync(Arg.Any<Expression<Func<BulkEntity, bool>>?>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<BulkEntity>>([]));
        repo.AddRangeAsync(Arg.Any<IEnumerable<BulkEntity>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<BulkEntity>>([]));

        var handler = new BulkUpsertCommandHandler<DummyDbContext, BulkEntity, int>(uow);
        var entities = Enumerable.Range(1, KyrolusBulkLimits.MaxBatchSize)
            .Select(i => new BulkEntity { Id = i, Name = $"n{i}" })
            .ToList();
        var command = new BulkUpsertCommand<BulkEntity, int>(entities, ["Id"]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Count().ShouldBe(KyrolusBulkLimits.MaxBatchSize);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Bug4 EF: ExecuteUpdateCommandHandler must always reject Key/Timestamp/ConcurrencyCheck/Computed properties

    [Theory(DisplayName = "Bug4 EF: ExecuteUpdateCommandHandler rejects Key/Timestamp/ConcurrencyCheck/Computed properties even without an AllowedProperties allow-list")]
    [InlineData(nameof(ProtectedPropsEntity.Id))]
    [InlineData(nameof(ProtectedPropsEntity.RowVersion))]
    [InlineData(nameof(ProtectedPropsEntity.Version))]
    [InlineData(nameof(ProtectedPropsEntity.Computed))]
    public async Task ExecuteUpdateCommandHandler_RejectsProtectedProperty_EvenWithoutAllowList(string propertyName)
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, ProtectedPropsEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, ProtectedPropsEntity, int>>().Returns(repo);

        Action<UpdateSettersBuilder<ProtectedPropsEntity>>? captured = null;
        repo.ExecuteUpdateAsync(
                Arg.Any<Expression<Func<ProtectedPropsEntity, bool>>?>(),
                Arg.Do<Action<UpdateSettersBuilder<ProtectedPropsEntity>>>(a => captured = a),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var handler = new ExecuteUpdateCommandHandler<DummyDbContext, ProtectedPropsEntity, int>(uow);
        // AllowedProperties is intentionally left unset - KyrolusPropertyAllowListBehavior is a
        // separate, opt-in pipeline behavior not exercised by this handler-level test at all, so
        // this proves the protection is independent of that allow-list.
        var command = new ExecuteUpdateCommand<ProtectedPropsEntity, int>(
            x => true,
            new Dictionary<string, object> { [propertyName] = GetSampleValue(propertyName) });

        await handler.Handle(command, CancellationToken.None);

        captured.ShouldNotBeNull();
        Should.Throw<KyrolusSecurityException>(() => captured!(new UpdateSettersBuilder<ProtectedPropsEntity>()));
    }

    [Fact(DisplayName = "Bug4 EF: ExecuteUpdateCommandHandler still allows an ordinary (non-protected) property")]
    public async Task ExecuteUpdateCommandHandler_AllowsOrdinaryProperty()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, ProtectedPropsEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, ProtectedPropsEntity, int>>().Returns(repo);

        Action<UpdateSettersBuilder<ProtectedPropsEntity>>? captured = null;
        repo.ExecuteUpdateAsync(
                Arg.Any<Expression<Func<ProtectedPropsEntity, bool>>?>(),
                Arg.Do<Action<UpdateSettersBuilder<ProtectedPropsEntity>>>(a => captured = a),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var handler = new ExecuteUpdateCommandHandler<DummyDbContext, ProtectedPropsEntity, int>(uow);
        var command = new ExecuteUpdateCommand<ProtectedPropsEntity, int>(x => true, new Dictionary<string, object> { ["Name"] = "Alice" });

        await handler.Handle(command, CancellationToken.None);

        captured.ShouldNotBeNull();
        Should.NotThrow(() => captured!(new UpdateSettersBuilder<ProtectedPropsEntity>()));
    }

    private static object GetSampleValue(string propertyName) => propertyName switch
    {
        nameof(ProtectedPropsEntity.Id) => 5,
        nameof(ProtectedPropsEntity.RowVersion) => new byte[] { 1, 2, 3 },
        nameof(ProtectedPropsEntity.Version) => 2,
        nameof(ProtectedPropsEntity.Computed) => "x",
        _ => "x"
    };

    #endregion
}
