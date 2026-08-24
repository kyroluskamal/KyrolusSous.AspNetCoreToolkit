using System.Linq.Expressions;
using KyrolusSous.Repositories.EF.Abstractions.Pagination;
using KyrolusSous.Repositories.EF.Runtime.Pagination;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class KeysetPaginationTests
{
    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private sealed class TestDbContext : DbContext
    {
        public DbSet<Product> Products => Set<Product>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }

    private sealed class ProductKeysetSpecification : IKyrolusKeysetSpecification<Product, int>
    {
        public Expression<Func<Product, int>> CursorSelector { get; } = p => p.Id;
        public int? CursorValue { get; init; }
        public KyrolusKeysetDirection Direction { get; init; } = KyrolusKeysetDirection.Forward;
        public int PageSize { get; init; } = 2;
        public Expression<Func<Product, bool>>? Filter { get; init; }
        public bool IsDescending { get; init; }
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TestDbContext(options);
        context.Products.AddRange(
            new Product { Id = 1, Name = "Product A", Price = 10m },
            new Product { Id = 2, Name = "Product B", Price = 20m },
            new Product { Id = 3, Name = "Product C", Price = 30m },
            new Product { Id = 4, Name = "Product D", Price = 40m },
            new Product { Id = 5, Name = "Product E", Price = 50m }
        );
        context.SaveChanges();
        return context;
    }

    [Fact(DisplayName = "KeysetPagination: Fetches first page forward accurately")]
    public async Task KeysetPagination_FirstPage_Forward()
    {
        using var context = CreateContext();
        var spec = new ProductKeysetSpecification { CursorValue = null, PageSize = 2, Direction = KyrolusKeysetDirection.Forward };

        var result = await context.Products.ToKeysetPageAsync(spec);

        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.Items[0].Id.ShouldBe(1);
        result.Items[1].Id.ShouldBe(2);
        result.HasNextPage.ShouldBeTrue();
        result.NextCursor.ShouldBe(2);
    }

    [Fact(DisplayName = "KeysetPagination: Fetches second page using cursor accurately")]
    public async Task KeysetPagination_SecondPage_WithCursor()
    {
        using var context = CreateContext();
        var spec = new ProductKeysetSpecification { CursorValue = 2, PageSize = 2, Direction = KyrolusKeysetDirection.Forward };

        var result = await context.Products.ToKeysetPageAsync(spec);

        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.Items[0].Id.ShouldBe(3);
        result.Items[1].Id.ShouldBe(4);
        result.HasNextPage.ShouldBeTrue();
        result.NextCursor.ShouldBe(4);
    }

    [Fact(DisplayName = "KeysetPagination: Fetches backward page accurately")]
    public async Task KeysetPagination_Backward()
    {
        using var context = CreateContext();
        var spec = new ProductKeysetSpecification { CursorValue = 4, PageSize = 2, Direction = KyrolusKeysetDirection.Backward };

        var result = await context.Products.ToKeysetPageAsync(spec);

        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.Items[0].Id.ShouldBe(2);
        result.Items[1].Id.ShouldBe(3);
    }
}
