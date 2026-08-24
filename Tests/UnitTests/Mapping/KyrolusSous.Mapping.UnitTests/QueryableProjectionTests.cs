namespace KyrolusSous.Mapping.UnitTests;

public sealed class QueryableProjectionTests
{
    private sealed class ProductEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string InternalSku { get; set; } = string.Empty;
    }

    private sealed class ProductSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [Fact(DisplayName = "KyrolusQueryableProjection: Builds LINQ expression and projects IQueryable sequence")]
    public void ProjectTo_ProjectsQueryableSequence()
    {
        var entities = new List<ProductEntity>
        {
            new() { Id = 1, Name = "Laptop", Price = 1200m, InternalSku = "SKU-01" },
            new() { Id = 2, Name = "Mouse", Price = 25m, InternalSku = "SKU-02" }
        }.AsQueryable();

        var projected = entities.ProjectTo<ProductEntity, ProductSummaryDto>().ToList();

        projected.Count.ShouldBe(2);
        projected[0].Id.ShouldBe(1);
        projected[0].Name.ShouldBe("Laptop");
        projected[0].Price.ShouldBe(1200m);
        projected[1].Name.ShouldBe("Mouse");
    }

    [Fact(DisplayName = "IKyrolusObjectMapper: GetProjection returns valid LambdaExpression")]
    public void GetProjection_ReturnsValidExpression()
    {
        var mapper = new KyrolusObjectMapper();
        var expr = mapper.GetProjection<ProductEntity, ProductSummaryDto>();

        expr.ShouldNotBeNull();
        var func = expr.Compile();
        var entity = new ProductEntity { Id = 99, Name = "Keyboard", Price = 80m };
        var dto = func(entity);

        dto.Id.ShouldBe(99);
        dto.Name.ShouldBe("Keyboard");
        dto.Price.ShouldBe(80m);
    }
}
