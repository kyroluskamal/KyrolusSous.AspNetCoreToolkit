using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Metadata;
using KyrolusSous.Repositories.Marten.Abstractions.Outbox;
using KyrolusSous.Repositories.Marten.Abstractions.Upcasting;
using KyrolusSous.Repositories.Marten.Runtime.Bulk;
using KyrolusSous.Repositories.Marten.Runtime.Dynamic;
using KyrolusSous.Repositories.Marten.Runtime.Metadata;
using KyrolusSous.Repositories.Marten.Runtime.Pagination;
using KyrolusSous.Repositories.Marten.Runtime.Repository;
using KyrolusSous.Repositories.Marten.Runtime.Saga;
using KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;
using KyrolusSous.Repositories.Marten.Runtime.Upcasting;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenLogicalHardeningTests
{
    public sealed class ItemDoc
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }

    [Fact(DisplayName = "Audit: UnitOfWork throws ObjectDisposedException when accessed after disposal")]
    public void UnitOfWork_ThrowsObjectDisposedException_AfterDisposal()
    {
        var session = Substitute.For<IDocumentSession>();
        var uow = new KyrolusMartenUnitOfWork<IDocumentSession>(session);

        uow.Dispose();

        Should.Throw<ObjectDisposedException>(() => uow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, ItemDoc, Guid>>());
        Should.ThrowAsync<ObjectDisposedException>(async () => await uow.SaveChangesAsync());
        Should.ThrowAsync<ObjectDisposedException>(async () => await uow.EnqueueAsync(new KyrolusMartenOutboxMessage()));
    }

    [Fact(DisplayName = "Audit: UnitOfWork implements IKyrolusMartenOutboxStore natively")]
    public async Task UnitOfWork_EnqueuesOutboxMessage_Directly()
    {
        var session = Substitute.For<IDocumentSession>();
        var uow = new KyrolusMartenUnitOfWork<IDocumentSession>(session);

        var msg = new KyrolusMartenOutboxMessage { Id = Guid.NewGuid(), Payload = "{}" };
        await uow.EnqueueAsync(msg);

        session.Received(1).Store(msg);
    }

    [Fact(DisplayName = "Audit: SoftDelete RestoreAsync restores and invalidates cache")]
    public async Task SoftDelete_RestoreAsync_SetsIsDeletedFalse()
    {
        var session = Substitute.For<IDocumentSession>();
        var repo = new KyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, ItemDoc, Guid>(session);

        var id = Guid.NewGuid();
        var existingDoc = new ItemDoc { Id = id, IsDeleted = true };
        session.LoadAsync<ItemDoc>(id, Arg.Any<CancellationToken>()).Returns(existingDoc);

        var restored = await repo.RestoreAsync(id);

        restored.ShouldBeTrue();
        existingDoc.IsDeleted.ShouldBeFalse();
        session.Received(1).Store(existingDoc);
    }

    [Fact(DisplayName = "Audit: Repository CRUD throws ArgumentNullException on null entity arguments")]
    public async Task Repository_CRUD_ThrowsOnNullArguments()
    {
        var session = Substitute.For<IDocumentSession>();
        var repo = new KyrolusMartenRepositoryAsync<IDocumentSession, ItemDoc, Guid>(session);

        await Should.ThrowAsync<ArgumentNullException>(async () => await repo.AddAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await repo.AddRangeAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await repo.UpdateAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await repo.RemoveAsync((ItemDoc)null!));
    }

    [Fact(DisplayName = "Audit: DynamicFilter safely handles Guid, DateOnly, and Enum values")]
    public void DynamicFilter_SafelyConvertsCustomTypes()
    {
        var id = Guid.NewGuid();
        var items = new List<ItemDoc>
        {
            new() { Id = id, Name = "Item1" },
            new() { Id = Guid.NewGuid(), Name = "Item2" }
        }.AsQueryable();

        var filtered = items.ApplyMartenDynamicFilter(nameof(ItemDoc.Id), "==", id.ToString()).ToList();
        filtered.Count.ShouldBe(1);
        filtered[0].Id.ShouldBe(id);
    }

    [Fact(DisplayName = "Audit: BulkInsertDocumentsAsync guards against null and empty")]
    public async Task BulkInsert_GuardsAgainstNullAndEmpty()
    {
        var store = Substitute.For<IDocumentStore>();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await store.BulkInsertDocumentsAsync<ItemDoc>(null!));

        var task = store.BulkInsertDocumentsAsync<ItemDoc>([]);
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact(DisplayName = "Audit: Upcasting pipeline detects cycles and throws InvalidOperationException")]
    public void UpcastingPipeline_CyclicDetection_Throws()
    {
        var upcaster = Substitute.For<IKyrolusMartenEventUpcaster>();
        upcaster.SourceEventType.Returns(typeof(string));
        upcaster.TargetEventType.Returns(typeof(string));
        upcaster.Upcast(Arg.Any<object>()).Returns("same");

        var pipeline = new KyrolusMartenUpcastingPipeline([upcaster]);

        Should.Throw<InvalidOperationException>(() => pipeline.Upcast("start"));
    }

    [Fact(DisplayName = "Audit: KeysetPagination throws ArgumentOutOfRangeException for non-positive pageSize")]
    public void KeysetPagination_ThrowsOnZeroPageSize()
    {
        var items = new List<ItemDoc>().AsQueryable();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            items.ToMartenKeysetPage(x => x.Id, cursor: null, pageSize: 0));
    }
}
