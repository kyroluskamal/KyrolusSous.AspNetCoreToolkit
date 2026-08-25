using System.Linq.Expressions;
using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusCqrsGenericHandlersTests
{
    public sealed class ProductDoc
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public sealed class DummyDbContext : DbContext;

    [Fact(DisplayName = "CQRS EF: CountQueryHandler handles query through UnitOfWork")]
    public async Task CountQueryHandler_ExecutesSuccessfully()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusRepositoryAsync<DummyDbContext, ProductDoc, Guid>>();

        uow.GetRepository<IKyrolusRepositoryAsync<DummyDbContext, ProductDoc, Guid>>().Returns(repo);

        repo.GetPagedAsync(Arg.Any<IKyrolusPagedQuerySpecification<ProductDoc, ProductDoc>>(), Arg.Any<CancellationToken>())
            .Returns((new List<ProductDoc>(), 42));

        var handler = new CountQueryHandler<DummyDbContext, ProductDoc, Guid>(uow);
        var query = new CountQuery<ProductDoc>();

        var count = await handler.Handle(query, CancellationToken.None);

        count.ShouldBe(42L);
    }

    [Fact(DisplayName = "CQRS EF: GetByIdQueryHandler retrieves item by ID")]
    public async Task GetByIdQueryHandler_RetrievesItem()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var repo = Substitute.For<IKyrolusSingleKeyRepositoryAsync<DummyDbContext, ProductDoc, Guid>>();

        uow.GetRepository<IKyrolusSingleKeyRepositoryAsync<DummyDbContext, ProductDoc, Guid>>().Returns(repo);

        var id = Guid.NewGuid();
        var product = new ProductDoc { Id = id, Title = "Laptop", Price = 999m };
        repo.GetByIdAsync(
            id,
            Arg.Any<List<string>?>(),
            Arg.Any<IncludeGraph<ProductDoc>?>(),
            Arg.Any<bool?>(),
            Arg.Any<bool?>(),
            Arg.Any<CancellationToken>())
            .Returns(product);

        var handler = new GetByIdQueryHandler<DummyDbContext, ProductDoc, Guid>(uow);
        var query = new GetByIdQuery<ProductDoc, Guid>(id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.Title.ShouldBe("Laptop");
    }
}
