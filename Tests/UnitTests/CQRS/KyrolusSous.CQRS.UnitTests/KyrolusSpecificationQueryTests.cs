using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using Marten;
using Marten.Linq;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusSpecificationQueryTests
{
    public sealed class TestProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public sealed record TestProductNameDto(int Id, string Name);

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    [Fact(DisplayName = "Ef specification query handler should invoke repository query async")]
    public async Task Ef_specification_query_handler_should_invoke_repository_query_async()
    {
        var unitOfWork = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<TestDbContext, TestProduct, int>>();
        var spec = Substitute.For<IKyrolusQuerySpecification<TestProduct, TestProduct>>();

        unitOfWork.GetRepository<IKyrolusRepositoryAsync<TestDbContext, TestProduct, int>>().Returns(repo);
        var expected = new List<TestProduct> { new() { Id = 1, Name = "Laptop", Price = 1200m } };
        repo.QueryAsync(spec, Arg.Any<CancellationToken>()).Returns(expected);

        var handler = new SpecificationQueryHandler<TestDbContext, TestProduct, TestProduct, int>(unitOfWork);
        var query = new SpecificationQuery<TestProduct, TestProduct>(spec);

        var results = await handler.Handle(query, CancellationToken.None);

        results.ShouldBe(expected);
        await repo.Received(1).QueryAsync(spec, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Marten specification query handler should invoke repository get all async when TResult is TEntity (regression)")]
    public async Task Marten_specification_query_handler_should_invoke_repository_get_all_async()
    {
        var session = Substitute.For<IDocumentSession>();
        var unitOfWork = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, TestProduct, int>>();
        var spec = Substitute.For<IKyrolusQuerySpecification<TestProduct>>();

        unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, TestProduct, int>>().Returns(repo);
        var expected = new List<TestProduct> { new() { Id = 2, Name = "Phone", Price = 800m } };
        repo.GetAllAsync(Arg.Is<MartenQueryOptions<TestProduct>>(o => o.Specification == spec), Arg.Any<CancellationToken>())
            .Returns(expected);

        // TResult = TEntity, no Selector - this is the exact shape the old, single-generic
        // MartenSpecificationQuery<TEntity> used to construct, so it must keep going through
        // GetAllAsync (never the projected QueryAsync path) and return the entities untouched.
        var handler = new MartenSpecificationQueryHandler<IDocumentSession, TestProduct, TestProduct, int>(unitOfWork);
        var query = new MartenSpecificationQuery<TestProduct, TestProduct>(spec, tenantId: "tenant-1");

        var results = await handler.Handle(query, CancellationToken.None);

        results.ShouldBe(expected);
        await repo.Received(1).GetAllAsync(Arg.Any<MartenQueryOptions<TestProduct>>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().QueryAsync<TestProduct>(
            Arg.Any<MartenQueryOptions<TestProduct>?>(),
            Arg.Any<Func<IMartenQueryable<TestProduct>, IMartenQueryable<TestProduct>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Marten specification query handler should project through Selector when TResult differs from TEntity")]
    public async Task Marten_specification_query_handler_should_project_through_selector()
    {
        var unitOfWork = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, TestProduct, int>>();
        var spec = Substitute.For<IKyrolusQuerySpecification<TestProduct>>();

        unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, TestProduct, int>>().Returns(repo);
        var expected = new List<TestProductNameDto> { new(3, "Tablet") };
        repo.QueryAsync<TestProductNameDto>(
                Arg.Is<MartenQueryOptions<TestProduct>>(o => o.Specification == spec),
                Arg.Any<Func<IMartenQueryable<TestProduct>, IMartenQueryable<TestProductNameDto>>>(),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var handler = new MartenSpecificationQueryHandler<IDocumentSession, TestProduct, TestProductNameDto, int>(unitOfWork);
        var query = new MartenSpecificationQuery<TestProduct, TestProductNameDto>(spec, selector: p => new TestProductNameDto(p.Id, p.Name));

        var results = await handler.Handle(query, CancellationToken.None);

        results.ShouldBe(expected);
        await repo.Received(1).QueryAsync<TestProductNameDto>(
            Arg.Any<MartenQueryOptions<TestProduct>?>(),
            Arg.Any<Func<IMartenQueryable<TestProduct>, IMartenQueryable<TestProductNameDto>>>(),
            Arg.Any<CancellationToken>());
        await repo.DidNotReceive().GetAllAsync(Arg.Any<MartenQueryOptions<TestProduct>?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Marten specification query handler should throw when TResult differs from TEntity and no Selector is supplied")]
    public async Task Marten_specification_query_handler_should_throw_when_projection_required_but_missing()
    {
        var unitOfWork = Substitute.For<IKyrolusMartenUnitOfWork<IDocumentSession>>();
        var repo = Substitute.For<IKyrolusMartenRepositoryAsync<IDocumentSession, TestProduct, int>>();
        var spec = Substitute.For<IKyrolusQuerySpecification<TestProduct>>();
        unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, TestProduct, int>>().Returns(repo);

        var handler = new MartenSpecificationQueryHandler<IDocumentSession, TestProduct, TestProductNameDto, int>(unitOfWork);
        var query = new MartenSpecificationQuery<TestProduct, TestProductNameDto>(spec);

        // Without a Selector there is no way to turn a TestProduct into a TestProductNameDto - this
        // must fail closed instead of throwing an unrelated cast exception or silently returning the
        // wrong shape.
        await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(query, CancellationToken.None));
        await repo.DidNotReceive().GetAllAsync(Arg.Any<MartenQueryOptions<TestProduct>?>(), Arg.Any<CancellationToken>());
    }
}
