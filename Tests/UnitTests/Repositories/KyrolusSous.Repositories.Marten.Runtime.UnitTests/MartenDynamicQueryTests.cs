using KyrolusSous.Repositories.Marten.Runtime.Dynamic;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenDynamicQueryTests
{
    private sealed class ProductDoc
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateOnly ReleaseDate { get; set; }
    }

    [Fact(DisplayName = "DynamicQuery: ApplyMartenDynamicFilter filters records properly")]
    public void ApplyMartenDynamicFilter_FiltersInMemoryQueryable()
    {
        var items = new List<ProductDoc>
        {
            new() { Id = Guid.NewGuid(), Title = "Laptop", Price = 1200m, Stock = 10, ReleaseDate = new DateOnly(2025, 1, 1) },
            new() { Id = Guid.NewGuid(), Title = "Mouse", Price = 25m, Stock = 100, ReleaseDate = new DateOnly(2025, 2, 1) },
            new() { Id = Guid.NewGuid(), Title = "Keyboard", Price = 75m, Stock = 50, ReleaseDate = new DateOnly(2025, 3, 1) }
        }.AsQueryable();

        var filtered = items.ApplyMartenDynamicFilter(nameof(ProductDoc.Price), ">", 50m).ToList();
        filtered.Count.ShouldBe(2);
        filtered.ShouldContain(p => p.Title == "Laptop");
        filtered.ShouldContain(p => p.Title == "Keyboard");
    }

    [Fact(DisplayName = "DynamicQuery: ApplyMartenDynamicSort sorts records properly")]
    public void ApplyMartenDynamicSort_SortsInMemoryQueryable()
    {
        var items = new List<ProductDoc>
        {
            new() { Id = Guid.NewGuid(), Title = "Laptop", Price = 1200m, Stock = 10 },
            new() { Id = Guid.NewGuid(), Title = "Mouse", Price = 25m, Stock = 100 },
            new() { Id = Guid.NewGuid(), Title = "Keyboard", Price = 75m, Stock = 50 }
        }.AsQueryable();

        var sorted = items.ApplyMartenDynamicSort("Price desc").ToList();
        sorted.Count.ShouldBe(3);
        sorted[0].Title.ShouldBe("Laptop");
        sorted[1].Title.ShouldBe("Keyboard");
        sorted[2].Title.ShouldBe("Mouse");
    }
}
