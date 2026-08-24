using KyrolusSous.Repositories.EF.Abstractions.Dynamic;
using KyrolusSous.Repositories.EF.Runtime.Dynamic;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class DynamicQueryTests
{
    private sealed class Item
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [Fact(DisplayName = "DynamicQueryExtensions: Applies dynamic string sort correctly")]
    public void ApplyDynamicSort_SortsAscendingAndDescending()
    {
        var items = new List<Item>
        {
            new() { Id = 1, Title = "B", Price = 20m },
            new() { Id = 2, Title = "A", Price = 30m },
            new() { Id = 3, Title = "C", Price = 10m }
        }.AsQueryable();

        var sortedByPriceDesc = items.ApplyDynamicSort("Price desc").ToList();
        sortedByPriceDesc[0].Price.ShouldBe(30m);
        sortedByPriceDesc[1].Price.ShouldBe(20m);
        sortedByPriceDesc[2].Price.ShouldBe(10m);

        var sortedByTitleAsc = items.ApplyDynamicSort("Title asc").ToList();
        sortedByTitleAsc[0].Title.ShouldBe("A");
        sortedByTitleAsc[1].Title.ShouldBe("B");
        sortedByTitleAsc[2].Title.ShouldBe("C");
    }

    [Fact(DisplayName = "DynamicQueryExtensions: Applies dynamic binary filter correctly")]
    public void ApplyDynamicFilter_FiltersByPredicate()
    {
        var items = new List<Item>
        {
            new() { Id = 1, Title = "Laptop", Price = 1000m },
            new() { Id = 2, Title = "Mouse", Price = 50m },
            new() { Id = 3, Title = "Keyboard", Price = 150m }
        }.AsQueryable();

        var expensive = items.ApplyDynamicFilter("Price", KyrolusFilterOperator.GreaterThan, 100m).ToList();
        expensive.Count.ShouldBe(2);

        var containsTop = items.ApplyDynamicFilter("Title", KyrolusFilterOperator.Contains, "top").ToList();
        containsTop.Count.ShouldBe(1);
        containsTop[0].Title.ShouldBe("Laptop");
    }
}
