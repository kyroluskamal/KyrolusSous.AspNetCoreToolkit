namespace KyrolusSous.Mapping.UnitTests;

public sealed class CollectionMappingTests
{
    private sealed class Item
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    private sealed class ItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    private sealed class Cart
    {
        public int CartId { get; set; }
        public List<Item> Items { get; set; } = new();
        public string[] Tags { get; set; } = [];
    }

    private sealed class CartDto
    {
        public int CartId { get; set; }
        public List<ItemDto> Items { get; set; } = new();
        public HashSet<string> Tags { get; set; } = new();
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Maps collections within objects (List, Array, HashSet)")]
    public void CollectionProperties_Mapping_Works()
    {
        var mapper = new KyrolusObjectMapper();
        var cart = new Cart
        {
            CartId = 100,
            Items =
            [
                new Item { Id = 1, Title = "Keyboard" },
                new Item { Id = 2, Title = "Mouse" }
            ],
            Tags = ["electronics", "gaming"]
        };

        var dto = mapper.Map<Cart, CartDto>(cart);

        dto.ShouldNotBeNull();
        dto.CartId.ShouldBe(100);
        dto.Items.Count.ShouldBe(2);
        dto.Items[0].Title.ShouldBe("Keyboard");
        dto.Items[1].Title.ShouldBe("Mouse");
        dto.Tags.Count.ShouldBe(2);
        dto.Tags.ShouldContain("electronics");
        dto.Tags.ShouldContain("gaming");
    }

    [Fact(DisplayName = "KyrolusObjectMapper: MapEnumerable and MapList convert sequences with pre-allocation")]
    public void TopLevel_MapEnumerable_And_MapList()
    {
        var mapper = new KyrolusObjectMapper();
        var items = new List<Item>
        {
            new() { Id = 1, Title = "Monitor" },
            new() { Id = 2, Title = "Desk" }
        };

        var enumerableResult = mapper.MapEnumerable<Item, ItemDto>(items).ToList();
        enumerableResult.Count.ShouldBe(2);
        enumerableResult[0].Title.ShouldBe("Monitor");

        var listResult = mapper.MapList<Item, ItemDto>(items);
        listResult.Count.ShouldBe(2);
        listResult[1].Title.ShouldBe("Desk");
    }
}
