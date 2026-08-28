using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using Marten;
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

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    [Fact]
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

    [Fact]
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

        var handler = new MartenSpecificationQueryHandler<IDocumentSession, TestProduct, int>(unitOfWork);
        var query = new MartenSpecificationQuery<TestProduct>(spec, tenantId: "tenant-1");

        var results = await handler.Handle(query, CancellationToken.None);

        results.ShouldBe(expected);
        await repo.Received(1).GetAllAsync(Arg.Any<MartenQueryOptions<TestProduct>>(), Arg.Any<CancellationToken>());
    }
}
