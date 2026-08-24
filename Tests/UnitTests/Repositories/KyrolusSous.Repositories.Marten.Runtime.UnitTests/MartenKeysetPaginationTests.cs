using KyrolusSous.Repositories.Marten.Runtime.Pagination;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenKeysetPaginationTests
{
    private sealed class ProductItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "KeysetPagination: Fetches first page and calculates NextCursor correctly")]
    public void ToMartenKeysetPage_FirstPage_ReturnsCorrectItemsAndCursor()
    {
        var items = Enumerable.Range(1, 10)
            .Select(i => new ProductItem { Id = i, Name = $"Product {i}" })
            .AsQueryable();

        var page = items.ToMartenKeysetPage(x => x.Id, cursor: null, pageSize: 3);

        page.Items.Count.ShouldBe(3);
        page.HasNext.ShouldBeTrue();
        page.NextCursor.ShouldBe(3);
        page.Items[0].Id.ShouldBe(1);
        page.Items[2].Id.ShouldBe(3);
    }

    [Fact(DisplayName = "KeysetPagination: Fetches subsequent page starting from cursor")]
    public void ToMartenKeysetPage_SecondPage_StartsAfterCursor()
    {
        var items = Enumerable.Range(1, 10)
            .Select(i => new ProductItem { Id = i, Name = $"Product {i}" })
            .AsQueryable();

        var page = items.ToMartenKeysetPage(x => x.Id, cursor: 3, pageSize: 3);

        page.Items.Count.ShouldBe(3);
        page.HasNext.ShouldBeTrue();
        page.NextCursor.ShouldBe(6);
        page.Items[0].Id.ShouldBe(4);
        page.Items[2].Id.ShouldBe(6);
    }

    [Fact(DisplayName = "KeysetPagination: Last page sets HasNext to false")]
    public void ToMartenKeysetPage_LastPage_HasNextFalse()
    {
        var items = Enumerable.Range(1, 5)
            .Select(i => new ProductItem { Id = i, Name = $"Product {i}" })
            .AsQueryable();

        var page = items.ToMartenKeysetPage(x => x.Id, cursor: 3, pageSize: 3);

        page.Items.Count.ShouldBe(2);
        page.HasNext.ShouldBeFalse();
        page.Items[0].Id.ShouldBe(4);
        page.Items[1].Id.ShouldBe(5);
    }
}
