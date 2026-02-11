namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    public enum CaseId { Any_Electronics, All_Books }
    public static TheoryData<CaseId> CollectionNavCases => [CaseId.Any_Electronics, CaseId.All_Books];
    private static readonly IReadOnlyDictionary<CaseId, CaseSpec> Specs =
    new Dictionary<CaseId, CaseSpec>
    {
        [CaseId.Any_Electronics] = new CaseSpec(
            Op: "any",
            CategoryId: DataSeeder.categoryElectronicsId,
            ExpectedCount: 2,
            Assert: products =>
            {
                products.All(p =>
                    p.ProductCategories.Any(pc => pc.CategoryId == DataSeeder.categoryElectronicsId)
                ).ShouldBeTrue();
            }),

        [CaseId.All_Books] = new CaseSpec(
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
    public async Task GetAllIncludingDeletedAsync_AnyAll_Operator_Works(CaseId caseId)
    {
        var spec = Specs[caseId];
        var request = new QueryRequest(
                                        Filters: [new FilterClause(nameof(Product.ProductCategories), spec.Op, $"{nameof(ProductCategory.CategoryId)} = {spec.CategoryId}")],
                                        Includes: [nameof(Product.ProductCategories)]
                                    );

        await WithSoftDeletedAsync_SingleKey<Product>(
            DataSeeder.productLaptopId,
            async (_, products, _, _, _) =>
            {
                products.ShouldNotBeNull();
                products.Count.ShouldBe(spec.ExpectedCount);
                spec.Assert(products);
            },
            request
        );
    }
}
