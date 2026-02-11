namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    public static TheoryData<string> CollectionNavCases => new() { "any-electronics", "all-books" };
    private static readonly IReadOnlyDictionary<string, CaseSpec> Specs =
    new Dictionary<string, CaseSpec>
    {
        ["any-electronics"] = new CaseSpec(
            Op: "any",
            CategoryId: DataSeeder.categoryElectronicsId,
            ExpectedCount: 2,
            Assert: products =>
            {
                products.All(p =>
                    p.ProductCategories.Any(pc => pc.CategoryId == DataSeeder.categoryElectronicsId)
                ).ShouldBeTrue();
            }),

        ["all-books"] = new CaseSpec(
            Op: "all",
            CategoryId: DataSeeder.categoryBooksId,
            ExpectedCount: 1,
            Assert: products =>
            {
                products[0].Name.ShouldBe("Clean Code");
                products[0].ProductCategories.ShouldNotBeNull();
            })
    };

    private sealed record CaseSpec(
        string Op,
        Guid CategoryId,
        int ExpectedCount,
        Action<List<Product>> Assert);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports any/all operator for collection navigation")]
    [MemberData(nameof(CollectionNavCases))]
    public async Task GetAllIncludingDeletedAsync_AnyAll_Operator_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        Specs.ContainsKey(caseId).ShouldBeTrue();
        var spec = Specs[caseId];
        var request = new QueryRequest(
                                        Filters: [new FilterClause(nameof(Product.ProductCategories), spec.Op, $"{nameof(ProductCategory.CategoryId)} = {spec.CategoryId}")],
                                        Includes: [nameof(Product.ProductCategories)]
                                    );

        await AssertProducts(request, products =>
        {
            products.Count.ShouldBe(spec.ExpectedCount);
            spec.Assert(products);
        });
    }
}
