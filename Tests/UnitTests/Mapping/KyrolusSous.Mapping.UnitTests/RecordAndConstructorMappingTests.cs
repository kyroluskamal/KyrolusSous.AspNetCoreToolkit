namespace KyrolusSous.Mapping.UnitTests;

public sealed class RecordAndConstructorMappingTests
{
    private sealed class ProductEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private sealed record ProductRecordDto(int Id, string Name, decimal Price);

    private sealed class RichEntity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;

        public RichEntity() { }

        [KyrolusMapConstructor]
        public RichEntity(int id, string title)
        {
            Id = id * 10; // Custom constructor side-effect
            Title = $"Formatted: {title}";
        }
    }

    private sealed class SimpleDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Maps to Positional Records using primary constructor")]
    public void PositionalRecord_Mapping_Works()
    {
        var mapper = new KyrolusObjectMapper();
        var entity = new ProductEntity { Id = 5, Name = "Smartphone", Price = 999.99m };

        var recordDto = mapper.Map<ProductEntity, ProductRecordDto>(entity);

        recordDto.ShouldNotBeNull();
        recordDto.Id.ShouldBe(5);
        recordDto.Name.ShouldBe("Smartphone");
        recordDto.Price.ShouldBe(999.99m);
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Respects [KyrolusMapConstructor] attribute for target instantiation")]
    public void MapConstructor_Attribute_Invoked()
    {
        var mapper = new KyrolusObjectMapper();
        var dto = new SimpleDto { Id = 3, Title = "Article" };

        var rich = mapper.Map<SimpleDto, RichEntity>(dto);

        rich.ShouldNotBeNull();
        rich.Id.ShouldBe(30); // 3 * 10
        rich.Title.ShouldBe("Formatted: Article");
    }
}
