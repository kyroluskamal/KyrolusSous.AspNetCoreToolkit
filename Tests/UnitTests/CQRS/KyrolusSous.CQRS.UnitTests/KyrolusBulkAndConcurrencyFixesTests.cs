using System.Linq.Expressions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using Marten;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

using EfBulk = KyrolusSous.CQRS.EF.Command.Bulk;
using MartenBulk = KyrolusSous.CQRS.Marten.Command.Bulk;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Regression tests for: the missing batch cap/cancellation checks on BulkPatchCommandHandler (EF and
/// Marten), Marten's BulkPatchCommandHandler throwing on a null Items list instead of returning 0, and
/// the missing protected-property guard on Marten's ExecuteUpdateCommandHandler.
/// </summary>
public sealed class KyrolusBulkAndConcurrencyFixesTests
{
    public sealed class DummyDbContext : DbContext;

    public sealed class BulkPatchEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class MartenBulkEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Implements <see cref="JasperFx.Metadata.IVersioned"/> - one of Marten's three version-tracking
    /// marker interfaces (see <c>ExecuteUpdateCommandHandler.IsProtectedFromUpdate</c>'s remarks for all
    /// three) - which is sufficient to prove the "Version" protection engages; a real document would
    /// normally implement at most one of the three anyway, since they use incompatible property types
    /// (Guid/int/long) for the same "Version" name.
    /// </summary>
    public sealed class MartenProtectedPropsEntity : JasperFx.Metadata.IVersioned
    {
        public Guid Id { get; set; }

        [JasperFx.Identity]
        public string ExternalKey { get; set; } = string.Empty;

        public Guid Version { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    // Deliberately does NOT implement IVersioned/IRevisioned/ILongVersioned - proves the "Version"
    // protection is scoped to types that actually opt into one of Marten's version-tracking interfaces,
    // not to every type with a same-named property.
    public sealed class MartenPlainEntityWithVersionProperty
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    #region Fix2 EF: BulkPatchCommandHandler must cap batch size and honor cancellation between items

    [Fact(DisplayName = "Fix2 EF: BulkPatchCommandHandler rejects a batch larger than KyrolusBulkLimits.MaxBatchSize")]
    public async Task EfBulkPatchCommandHandler_RejectsOversizedBatch()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var handler = new EfBulk.BulkPatchCommandHandler<DummyDbContext, BulkPatchEntity, int>(uow);
        var items = Enumerable.Range(1, EfBulk.KyrolusBulkLimits.MaxBatchSize + 1)
            .Select(i => new EfBulk.KyrolusBulkPatchItem([i], new Dictionary<string, object> { ["Name"] = $"n{i}" }))
            .ToList();
        var command = new EfBulk.BulkPatchCommand<BulkPatchEntity, int>(items);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        ex.Message.ShouldContain(EfBulk.KyrolusBulkLimits.MaxBatchSize.ToString());
    }

    [Fact(DisplayName = "Fix2 EF: BulkPatchCommandHandler honors cancellation between items")]
    public async Task EfBulkPatchCommandHandler_CancellationMidLoop_Honored()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusSingleKeyRepositoryAsync<DummyDbContext, BulkPatchEntity, int>>();
        uow.GetRepository<IKyrolusSingleKeyRepositoryAsync<DummyDbContext, BulkPatchEntity, int>>().Returns(repo);

        using var cts = new CancellationTokenSource();
        var callCount = 0;
        repo.PatchAsync(Arg.Any<int>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1) cts.Cancel();
                return Task.FromResult<BulkPatchEntity?>(null);
            });

        var handler = new EfBulk.BulkPatchCommandHandler<DummyDbContext, BulkPatchEntity, int>(uow);
        var items = new List<EfBulk.KyrolusBulkPatchItem>
        {
            new([1], new Dictionary<string, object> { ["Name"] = "n1" }),
            new([2], new Dictionary<string, object> { ["Name"] = "n2" })
        };
        var command = new EfBulk.BulkPatchCommand<BulkPatchEntity, int>(items);

        // The second item's PatchAsync must never run: cancellation raised during the first item's
        // call is observed by ThrowIfCancellationRequested at the top of the second loop iteration.
        await Should.ThrowAsync<OperationCanceledException>(() => handler.Handle(command, cts.Token));

        callCount.ShouldBe(1);
    }

    #endregion

    #region Fix2/Fix3 Marten: BulkPatchCommandHandler must cap batch size, honor cancellation, and tolerate a null Items list

    [Fact(DisplayName = "Fix2 Marten: BulkPatchCommandHandler rejects a batch larger than KyrolusBulkLimits.MaxBatchSize")]
    public async Task MartenBulkPatchCommandHandler_RejectsOversizedBatch()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var handler = new MartenBulk.BulkPatchCommandHandler<IDocumentSession, MartenBulkEntity, Guid>(martenUow);
        var items = Enumerable.Range(1, MartenBulk.KyrolusBulkLimits.MaxBatchSize + 1)
            .Select(i => new MartenBulk.KyrolusBulkPatchItem([Guid.NewGuid()], new Dictionary<string, object> { ["Name"] = $"n{i}" }))
            .ToList();
        var command = new MartenBulk.BulkPatchCommand<MartenBulkEntity, Guid>(items);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        ex.Message.ShouldContain(MartenBulk.KyrolusBulkLimits.MaxBatchSize.ToString());
    }

    [Fact(DisplayName = "Fix2 Marten: BulkPatchCommandHandler honors cancellation between items")]
    public async Task MartenBulkPatchCommandHandler_CancellationMidLoop_Honored()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenBulkEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenBulkEntity, Guid>>().Returns(repo);

        using var cts = new CancellationTokenSource();
        var callCount = 0;
        repo.PatchWhereAsync(Arg.Any<Expression<Func<MartenBulkEntity, bool>>>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1) cts.Cancel();
                return Task.FromResult(1);
            });

        var handler = new MartenBulk.BulkPatchCommandHandler<IDocumentSession, MartenBulkEntity, Guid>(martenUow);
        var items = new List<MartenBulk.KyrolusBulkPatchItem>
        {
            new([Guid.NewGuid()], new Dictionary<string, object> { ["Name"] = "n1" }),
            new([Guid.NewGuid()], new Dictionary<string, object> { ["Name"] = "n2" })
        };
        var command = new MartenBulk.BulkPatchCommand<MartenBulkEntity, Guid>(items);

        await Should.ThrowAsync<OperationCanceledException>(() => handler.Handle(command, cts.Token));

        callCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Fix3 Marten: BulkPatchCommandHandler returns 0 for a null Items list instead of throwing")]
    public async Task MartenBulkPatchCommandHandler_NullItems_ReturnsZero()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var handler = new MartenBulk.BulkPatchCommandHandler<IDocumentSession, MartenBulkEntity, Guid>(martenUow);
        // Only reachable via a reflection-based command builder bypassing the compiler's non-null
        // annotation on Items - the same scenario EF's equivalent `?? []` guard exists for.
        var command = new MartenBulk.BulkPatchCommand<MartenBulkEntity, Guid>(items: null!);

        var result = await handler.Handle(command, CancellationToken.None);

        result.ShouldBe(0);
        await martenUow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Fix4 Marten: ExecuteUpdateCommandHandler must reject the document identity and version/revision properties

    [Theory(DisplayName = "Fix4 Marten: ExecuteUpdateCommandHandler rejects Id/[Identity]/Version properties even without an AllowedProperties allow-list")]
    [InlineData(nameof(MartenProtectedPropsEntity.Id))]
    [InlineData(nameof(MartenProtectedPropsEntity.ExternalKey))]
    [InlineData(nameof(MartenProtectedPropsEntity.Version))]
    public async Task MartenExecuteUpdateCommandHandler_RejectsProtectedProperty_EvenWithoutAllowList(string propertyName)
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenProtectedPropsEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenProtectedPropsEntity, Guid>>().Returns(repo);

        var handler = new MartenBulk.ExecuteUpdateCommandHandler<IDocumentSession, MartenProtectedPropsEntity, Guid>(martenUow);
        // AllowedProperties is intentionally left unset - this proves the protection is independent of
        // that separate, opt-in pipeline-level allow-list.
        var command = new MartenBulk.ExecuteUpdateCommand<MartenProtectedPropsEntity, Guid>(
            x => true,
            new Dictionary<string, object> { [propertyName] = GetSampleValue(propertyName) });

        await Should.ThrowAsync<KyrolusSecurityException>(() => handler.Handle(command, CancellationToken.None));

        await repo.DidNotReceive().PatchWhereAsync(
            Arg.Any<Expression<Func<MartenProtectedPropsEntity, bool>>>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fix4 Marten: ExecuteUpdateCommandHandler still allows an ordinary (non-protected) property")]
    public async Task MartenExecuteUpdateCommandHandler_AllowsOrdinaryProperty()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenProtectedPropsEntity, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenProtectedPropsEntity, Guid>>().Returns(repo);
        repo.PatchWhereAsync(
                Arg.Any<Expression<Func<MartenProtectedPropsEntity, bool>>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var handler = new MartenBulk.ExecuteUpdateCommandHandler<IDocumentSession, MartenProtectedPropsEntity, Guid>(martenUow);
        var command = new MartenBulk.ExecuteUpdateCommand<MartenProtectedPropsEntity, Guid>(x => true, new Dictionary<string, object> { ["Name"] = "Alice" });

        var affected = await handler.Handle(command, CancellationToken.None);

        affected.ShouldBe(1);
    }

    [Fact(DisplayName = "Fix4 Marten: ExecuteUpdateCommandHandler allows a 'Version' property when the document doesn't implement any Marten version-tracking interface")]
    public async Task MartenExecuteUpdateCommandHandler_VersionPropertyOnPlainType_IsNotProtected()
    {
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenPlainEntityWithVersionProperty, Guid>>();
        martenUow.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MartenPlainEntityWithVersionProperty, Guid>>().Returns(repo);
        repo.PatchWhereAsync(
                Arg.Any<Expression<Func<MartenPlainEntityWithVersionProperty, bool>>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        var handler = new MartenBulk.ExecuteUpdateCommandHandler<IDocumentSession, MartenPlainEntityWithVersionProperty, Guid>(martenUow);
        var command = new MartenBulk.ExecuteUpdateCommand<MartenPlainEntityWithVersionProperty, Guid>(x => true, new Dictionary<string, object> { ["Version"] = "v2" });

        var affected = await handler.Handle(command, CancellationToken.None);

        affected.ShouldBe(1);
    }

    private static object GetSampleValue(string propertyName) => propertyName switch
    {
        nameof(MartenProtectedPropsEntity.Id) => Guid.NewGuid(),
        nameof(MartenProtectedPropsEntity.ExternalKey) => "ext-1",
        nameof(MartenProtectedPropsEntity.Version) => Guid.NewGuid(),
        _ => "x"
    };

    #endregion
}
