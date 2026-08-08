namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private sealed record BasicSpec(
        bool IsComposite,
        bool UseExpressionOverload,
        object[] KeyValues,
        bool ExpectFound,
        Action<object>? AssertEntity);

    private static readonly IReadOnlyDictionary<string, BasicSpec> BasicSpecs = BuildBasicSpecs();

    public static TheoryData<string> BasicCases => CaseIdsFrom(BasicSpecs);

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync returns expected entities")]
    [MemberData(nameof(BasicCases))]
    public async Task GetByIdIncludingDeletedAsync_Basic_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = BasicSpecs[caseId];

        using var scope = Factory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var item = spec.UseExpressionOverload
                ? await repo.GetByIdIncludingDeletedAsync(spec.KeyValues, asNoTracking: true, useSplitQuery: null, cancellationToken: default)
                : await repo.GetByIdIncludingDeletedAsync(spec.KeyValues, includeProperties: null, includeGraph: null, asNoTracking: true, useSplitQuery: null, cancellationToken: default);

            if (!spec.ExpectFound)
            {
                item.ShouldBeNull();
                return;
            }

            item.ShouldNotBeNull();
            spec.AssertEntity?.Invoke(item!);
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var id = (Guid)spec.KeyValues[0];
        var single = spec.UseExpressionOverload
            ? await singleRepo.GetByIdIncludingDeletedAsync(id, asNoTracking: true, useSplitQuery: null, cancellationToken: default)
            : await singleRepo.GetByIdIncludingDeletedAsync(id, includeProperties: null, includeGraph: null, asNoTracking: true, useSplitQuery: null, cancellationToken: default);

        if (!spec.ExpectFound)
        {
            single.ShouldBeNull();
            return;
        }

        single.ShouldNotBeNull();
        spec.AssertEntity?.Invoke(single!);
    }

    private static IReadOnlyDictionary<string, BasicSpec> BuildBasicSpecs()
        => new Dictionary<string, BasicSpec>
        {
            ["single-list-found"] = new(
                IsComposite: false,
                UseExpressionOverload: false,
                KeyValues: [ExistingProductId],
                ExpectFound: true,
                AssertEntity: entity =>
                {
                    var product = (Product)entity;
                    product.Id.ShouldBe(ExistingProductId);
                    product.Name.ShouldBe("Laptop Pro 15");
                }),
            ["single-expression-found"] = new(
                IsComposite: false,
                UseExpressionOverload: true,
                KeyValues: [DataSeeder.productHeadphonesId],
                ExpectFound: true,
                AssertEntity: entity =>
                {
                    var product = (Product)entity;
                    product.Id.ShouldBe(DataSeeder.productHeadphonesId);
                    product.Price.ShouldBe(199m);
                }),
            ["single-list-missing"] = new(
                IsComposite: false,
                UseExpressionOverload: false,
                KeyValues: [MissingProductId],
                ExpectFound: false,
                AssertEntity: null),
            ["single-expression-missing"] = new(
                IsComposite: false,
                UseExpressionOverload: true,
                KeyValues: [MissingProductId],
                ExpectFound: false,
                AssertEntity: null),
            ["composite-list-found"] = new(
                IsComposite: true,
                UseExpressionOverload: false,
                KeyValues: ExistingReviewKey,
                ExpectFound: true,
                AssertEntity: entity =>
                {
                    var review = (Review)entity;
                    review.ProductId.ShouldBe(DataSeeder.productLaptopId);
                    review.CustomerId.ShouldBe(DataSeeder.customerJaneId);
                }),
            ["composite-expression-found"] = new(
                IsComposite: true,
                UseExpressionOverload: true,
                KeyValues: ExistingReviewKey,
                ExpectFound: true,
                AssertEntity: entity =>
                {
                    var review = (Review)entity;
                    review.Rating.ShouldBe(5);
                }),
            ["composite-list-missing"] = new(
                IsComposite: true,
                UseExpressionOverload: false,
                KeyValues: MissingReviewKey,
                ExpectFound: false,
                AssertEntity: null),
            ["composite-expression-missing"] = new(
                IsComposite: true,
                UseExpressionOverload: true,
                KeyValues: MissingReviewKey,
                ExpectFound: false,
                AssertEntity: null)
        };
}
