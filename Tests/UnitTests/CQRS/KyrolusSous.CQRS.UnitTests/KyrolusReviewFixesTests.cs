using System.Linq.Expressions;
using System.Reflection;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using Marten;
using Marten.Linq;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

using EfBehaviors = KyrolusSous.CQRS.EF.Behaviors;
using EfBulk = KyrolusSous.CQRS.EF.Command.Bulk;
using EfQuery = KyrolusSous.CQRS.EF.Query;
using MartenBehaviors = KyrolusSous.CQRS.Marten.Behaviors;
using MartenBulk = KyrolusSous.CQRS.Marten.Command.Bulk;
using MartenSoftDelete = KyrolusSous.CQRS.Marten.Command.SoftDelete;
using MartenQuery = KyrolusSous.CQRS.Marten.Query;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Regression tests for the security/logic review fixes: domain-events-before-commit ordering,
/// the ExecuteDelete/ExecuteUpdate null-filter guard, Marten seek pagination for Guid/string keys,
/// PageSize/PageNumber clamping, and the four previously-missing Marten handlers.
/// </summary>
public sealed class KyrolusReviewFixesTests
{
    public sealed class DummyDbContext : DbContext;

    public sealed class StubEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class GuidKeyedEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // ==========================================
    // Fix 1: PipelineOrder — Transaction must be INNER of the DomainEventsDispatch/
    // ReadModelProjection/LivePush cluster so commit/SaveChanges runs before those
    // behaviors' post-next() side effects.
    // ==========================================

    [Fact(DisplayName = "Fix1 PipelineOrder: EF Transaction (-530) is inner of DomainEventsDispatch(-650)/ReadModelProjection(-600)/LivePush(-550)")]
    public void EfTransactionBehavior_PipelineOrder_IsInnerOfEventCluster()
    {
        var transactionOrder = typeof(EfBehaviors.KyrolusEfTransactionBehavior<,,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var domainEventsOrder = typeof(EfBehaviors.KyrolusDomainEventsDispatchBehavior<,,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var projectionOrder = typeof(KyrolusReadModelProjectionBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var livePushOrder = typeof(KyrolusLivePushBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;

        transactionOrder.ShouldBe(-530);
        domainEventsOrder.ShouldBeLessThan(transactionOrder);
        projectionOrder.ShouldBeLessThan(transactionOrder);
        livePushOrder.ShouldBeLessThan(transactionOrder);
    }

    [Fact(DisplayName = "Fix1 PipelineOrder: Marten Transaction (-530) is inner of DomainEventsDispatch(-650)/ReadModelProjection(-600)/LivePush(-550)")]
    public void MartenTransactionBehavior_PipelineOrder_IsInnerOfEventCluster()
    {
        var transactionOrder = typeof(MartenBehaviors.KyrolusMartenTransactionBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var domainEventsOrder = typeof(MartenBehaviors.KyrolusMartenDomainEventsDispatchBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var projectionOrder = typeof(KyrolusReadModelProjectionBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var livePushOrder = typeof(KyrolusLivePushBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;

        transactionOrder.ShouldBe(-530);
        domainEventsOrder.ShouldBeLessThan(transactionOrder);
        projectionOrder.ShouldBeLessThan(transactionOrder);
        livePushOrder.ShouldBeLessThan(transactionOrder);
    }

    public sealed record CommitFailCmd(Guid Id) : IKyrolusCommand<bool>;

    public sealed class CommitFailEntity : IDomainEventSource
    {
        public Guid Id { get; set; }
        private readonly List<object> _events = [];
        public IReadOnlyCollection<object> DomainEvents => _events;
        public void AddDomainEvent(object e) => _events.Add(e);
        public void ClearDomainEvents() => _events.Clear();
    }

    public sealed class CommitFailDbContext(DbContextOptions<CommitFailDbContext> options) : DbContext(options)
    {
        public DbSet<CommitFailEntity> Entities => Set<CommitFailEntity>();
    }

    [Fact(DisplayName = "Fix1 EF: domain events are NOT observed when the nested transaction commit throws")]
    public async Task EfDomainEvents_NotDispatched_WhenInnerCommitFails()
    {
        var options = new DbContextOptionsBuilder<CommitFailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new CommitFailDbContext(options);

        var entity = new CommitFailEntity { Id = Guid.NewGuid() };
        entity.AddDomainEvent(new object());
        dbContext.Entities.Add(entity);

        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        // Post-fix, DomainEventsDispatchBehavior (-650) is OUTER and Transaction (-530) is nested
        // inside it. This `next` delegate stands in for "the handler ran, then the nested
        // transaction behavior's CommitAsync threw" — the realistic post-fix failure mode.
        var behavior = new EfBehaviors.KyrolusDomainEventsDispatchBehavior<CommitFailCmd, bool, CommitFailDbContext>(publisher, dbContext);

        var cmd = new CommitFailCmd(entity.Id);
        await Should.ThrowAsync<InvalidOperationException>(() =>
            behavior.Handle(cmd, _ => throw new InvalidOperationException("commit failed"), CancellationToken.None));

        await publisher.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    public sealed record MartenCommitFailCmd(Guid Id) : IKyrolusCommand<bool>, IDomainEventSource
    {
        private readonly List<object> _events = [];
        public IReadOnlyCollection<object> DomainEvents => _events;
        public void AddDomainEvent(object e) => _events.Add(e);
        public void ClearDomainEvents() => _events.Clear();
    }

    [Fact(DisplayName = "Fix1 Marten: domain events are NOT observed when the nested session SaveChanges throws")]
    public async Task MartenDomainEvents_NotDispatched_WhenInnerSaveFails()
    {
        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        var behavior = new MartenBehaviors.KyrolusMartenDomainEventsDispatchBehavior<MartenCommitFailCmd, bool>(publisher);

        var cmd = new MartenCommitFailCmd(Guid.NewGuid());
        cmd.AddDomainEvent(new object());

        await Should.ThrowAsync<InvalidOperationException>(() =>
            behavior.Handle(cmd, _ => throw new InvalidOperationException("save failed"), CancellationToken.None));

        await publisher.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ==========================================
    // Fix 2: ExecuteDeleteCommand/ExecuteUpdateCommand reject a null filter instead of
    // silently affecting every row.
    // ==========================================

    [Fact(DisplayName = "Fix2 EF: ExecuteDeleteCommand/ExecuteUpdateCommand constructors reject a null filter")]
    public void EfExecuteCommands_NullFilter_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new EfBulk.ExecuteDeleteCommand<StubEntity, int>(null!));
        Should.Throw<ArgumentNullException>(() => new EfBulk.ExecuteUpdateCommand<StubEntity, int>(null!, new Dictionary<string, object>()));
    }

    [Fact(DisplayName = "Fix2 Marten: ExecuteDeleteCommand/ExecuteUpdateCommand constructors reject a null filter")]
    public void MartenExecuteCommands_NullFilter_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new MartenBulk.ExecuteDeleteCommand<StubEntity, int>(null!));
        Should.Throw<ArgumentNullException>(() => new MartenBulk.ExecuteUpdateCommand<StubEntity, int>(null!, new Dictionary<string, object>()));
    }

    [Fact(DisplayName = "Fix2: an explicit x => true predicate is accepted for genuine whole-table operations")]
    public void ExecuteCommands_ExplicitTruePredicate_IsAccepted()
    {
        var efDelete = new EfBulk.ExecuteDeleteCommand<StubEntity, int>(x => true);
        efDelete.Filter.ShouldNotBeNull();

        var martenDelete = new MartenBulk.ExecuteDeleteCommand<StubEntity, int>(x => true);
        martenDelete.Filter.ShouldNotBeNull();
    }

    // ==========================================
    // Feature 1: PageSize/PageNumber clamping.
    // ==========================================

    [Fact(DisplayName = "Feature1 EF: GetPagedQueryHandler clamps a negative PageNumber and an oversized PageSize")]
    public async Task EfGetPagedQueryHandler_ClampsPageSizeAndPageNumber()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, StubEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, StubEntity, int>>().Returns(repo);

        repo.GetPagedWithDefaultsAsync<StubEntity>(default!)
            .ReturnsForAnyArgs(Task.FromResult(((IReadOnlyList<StubEntity>)new List<StubEntity>(), 0)));

        var handler = new EfQuery.GetPagedQueryHandler<DummyDbContext, StubEntity, int>(uow);
        var query = new EfQuery.GetPagedQuery<StubEntity, int>(pageNumber: -5, pageSize: int.MaxValue);

        var result = await handler.Handle(query, CancellationToken.None);

        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(200);
    }

    [Fact(DisplayName = "Feature1 EF: GetSeekQueryHandler clamps an oversized PageSize")]
    public async Task EfGetSeekQueryHandler_ClampsPageSize()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, StubEntity, int>>();
        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, StubEntity, int>>().Returns(repo);

        repo.QueryAsync(Arg.Any<IKyrolusQuerySpecification<StubEntity, StubEntity>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<StubEntity>()));

        var handler = new EfQuery.GetSeekQueryHandler<DummyDbContext, StubEntity, int>(uow);
        var query = new EfQuery.GetSeekQuery<StubEntity, int>(pageSize: int.MaxValue) { SeekPropertyNames = ["Id"] };

        var result = await handler.Handle(query, CancellationToken.None);

        result.PageSize.ShouldBe(200);
    }

    [Fact(DisplayName = "Feature1 Marten: GetPagedQueryHandler clamps a negative PageNumber and an oversized PageSize")]
    public async Task MartenGetPagedQueryHandler_ClampsPageSizeAndPageNumber()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, StubEntity, int>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, StubEntity, int>>().Returns(repo);

        repo.GetPageAsync(Arg.Any<MartenQueryOptions<StubEntity>?>(), Arg.Any<MartenPageRequest?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var page = (MartenPageRequest)callInfo[1]!;
                return Task.FromResult(new PageResult<StubEntity>([], 0, page.PageNumber, page.PageSize));
            });

        var handler = new MartenQuery.GetPagedQueryHandler<IDocumentSession, StubEntity, int>(martenUow);
        var query = new MartenQuery.GetPagedQuery<StubEntity, int>(pageNumber: -5, pageSize: int.MaxValue);

        var result = await handler.Handle(query, CancellationToken.None);

        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(200);
    }

    [Fact(DisplayName = "Feature1 Marten: GetSeekQueryHandler clamps an oversized PageSize")]
    public async Task MartenGetSeekQueryHandler_ClampsPageSize()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>().Returns(repo);

        repo.QueryAsync<GuidKeyedEntity>(
                Arg.Any<MartenQueryOptions<GuidKeyedEntity>?>(),
                Arg.Any<Func<IMartenQueryable<GuidKeyedEntity>, IMartenQueryable<GuidKeyedEntity>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<GuidKeyedEntity>>([]));

        var handler = new MartenQuery.GetSeekQueryHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);
        var query = new MartenQuery.GetSeekQuery<GuidKeyedEntity, Guid>(pageSize: int.MaxValue) { SeekPropertyNames = ["Id"] };

        var result = await handler.Handle(query, CancellationToken.None);

        result.PageSize.ShouldBe(200);
    }

    // ==========================================
    // Fix 3: Marten seek pagination must work for Guid (and other IComparable-but-no-operator)
    // key types instead of throwing via Expression.GreaterThan/LessThan.
    // ==========================================

    [Fact(DisplayName = "Fix3 Marten: seek pagination past page 1 for a Guid-keyed entity does not throw")]
    public async Task MartenGetSeekQueryHandler_GuidKey_Page2ViaCursor_DoesNotThrow()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>().Returns(repo);

        var item = new GuidKeyedEntity { Id = Guid.NewGuid(), Name = "A" };
        repo.QueryAsync<GuidKeyedEntity>(
                Arg.Any<MartenQueryOptions<GuidKeyedEntity>?>(),
                Arg.Any<Func<IMartenQueryable<GuidKeyedEntity>, IMartenQueryable<GuidKeyedEntity>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<GuidKeyedEntity>>([item]));

        var handler = new MartenQuery.GetSeekQueryHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);

        var page1Query = new MartenQuery.GetSeekQuery<GuidKeyedEntity, Guid>(pageSize: 1) { SeekPropertyNames = ["Id"] };
        var page1 = await handler.Handle(page1Query, CancellationToken.None);
        page1.NextToken.ShouldNotBeNull();

        var page2Query = new MartenQuery.GetSeekQuery<GuidKeyedEntity, Guid>(pageSize: 1, cursor: page1.NextToken) { SeekPropertyNames = ["Id"] };

        // Before the fix, building the cursor continuation predicate for a Guid property threw
        // InvalidOperationException("The binary operator GreaterThan is not defined for the types
        // 'System.Guid' and 'System.Guid'.") because TryBuildCompare used Expression.GreaterThan
        // instead of the CompareTo-via-reflection approach the EF provider already used correctly.
        var page2 = await handler.Handle(page2Query, CancellationToken.None);

        page2.ShouldNotBeNull();
    }

    // ==========================================
    // Feature 2: previously-missing Marten handlers for SoftDeleteByIdCommand,
    // RestoreByIdCommand, and the Bulk ExecuteDeleteCommand/ExecuteUpdateCommand.
    // ==========================================

    [Fact(DisplayName = "Feature2 Marten: SoftDeleteByIdCommandHandler soft-deletes via the soft-delete repository")]
    public async Task MartenSoftDeleteByIdCommandHandler_SoftDeletes()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>().Returns(repo);

        var id = Guid.NewGuid();
        repo.RemoveAsync(id, Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var handler = new MartenSoftDelete.SoftDeleteByIdCommandHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);
        var command = new MartenSoftDelete.SoftDeleteByIdCommand<GuidKeyedEntity, Guid>([id]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.ShouldBeTrue();
        await martenUow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Feature2 Marten: RestoreByIdCommandHandler restores via the soft-delete repository")]
    public async Task MartenRestoreByIdCommandHandler_Restores()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>().Returns(repo);

        var id = Guid.NewGuid();
        repo.RestoreAsync(id, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var handler = new MartenSoftDelete.RestoreByIdCommandHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);
        var command = new MartenSoftDelete.RestoreByIdCommand<GuidKeyedEntity, Guid>([id]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.ShouldBeTrue();
        await martenUow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Feature2 Marten: Bulk ExecuteDeleteCommandHandler calls DeleteWhereAsync and saves")]
    public async Task MartenExecuteDeleteCommandHandler_DeletesMatchingRows()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>().Returns(repo);
        repo.DeleteWhereAsync(Arg.Any<Expression<Func<GuidKeyedEntity, bool>>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(3));

        var handler = new MartenBulk.ExecuteDeleteCommandHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);
        var command = new MartenBulk.ExecuteDeleteCommand<GuidKeyedEntity, Guid>(x => x.Name == "stale");

        var affected = await handler.Handle(command, CancellationToken.None);

        affected.ShouldBe(3);
        await martenUow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Feature2 Marten: Bulk ExecuteUpdateCommandHandler calls PatchWhereAsync and saves")]
    public async Task MartenExecuteUpdateCommandHandler_PatchesMatchingRows()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, GuidKeyedEntity, Guid>>().Returns(repo);
        repo.PatchWhereAsync(Arg.Any<Expression<Func<GuidKeyedEntity, bool>>>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2));

        var handler = new MartenBulk.ExecuteUpdateCommandHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);
        var command = new MartenBulk.ExecuteUpdateCommand<GuidKeyedEntity, Guid>(x => x.Name == "stale", new Dictionary<string, object> { ["Name"] = "archived" });

        var affected = await handler.Handle(command, CancellationToken.None);

        affected.ShouldBe(2);
        await martenUow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Feature2 Marten: Bulk ExecuteUpdateCommandHandler is a no-op when Updates is empty")]
    public async Task MartenExecuteUpdateCommandHandler_NoUpdates_ReturnsZeroWithoutSaving()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var handler = new MartenBulk.ExecuteUpdateCommandHandler<IDocumentSession, GuidKeyedEntity, Guid>(martenUow);
        var command = new MartenBulk.ExecuteUpdateCommand<GuidKeyedEntity, Guid>(x => true, new Dictionary<string, object>());

        var affected = await handler.Handle(command, CancellationToken.None);

        affected.ShouldBe(0);
        await martenUow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
