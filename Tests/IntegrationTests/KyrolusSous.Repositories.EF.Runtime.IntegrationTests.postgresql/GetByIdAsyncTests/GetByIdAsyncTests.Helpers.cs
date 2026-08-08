namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    protected enum EntityKind
    {
        Product,
        Review
    }

    protected sealed record ByIdHttpSpec(
        EntityKind Kind,
        string? SingleKey,
        object?[]? CompositeKeys,
        QueryRequest? Request,
        HttpStatusCode ExpectedStatus,
        Action<Product>? AssertProduct,
        Action<Review>? AssertReview);

    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task RunByIdHttpCase(IReadOnlyDictionary<string, ByIdHttpSpec> specs, string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = specs[caseId];
        if (spec.Kind == EntityKind.Product)
        {
            var (response, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(
                spec.SingleKey!,
                spec.Request);
            response.StatusCode.ShouldBe(spec.ExpectedStatus);
            if (spec.ExpectedStatus == HttpStatusCode.OK)
            {
                product.ShouldNotBeNull();
                spec.AssertProduct?.Invoke(product!);
            }
            else
            {
                product.ShouldBeNull();
            }
            return;
        }

        var (reviewResponse, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(
            spec.CompositeKeys!,
            spec.Request);
        reviewResponse.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (spec.ExpectedStatus == HttpStatusCode.OK)
        {
            review.ShouldNotBeNull();
            spec.AssertReview?.Invoke(review!);
        }
        else
        {
            review.ShouldBeNull();
        }
    }

    private static string productLaptopId => DataSeeder.productLaptopId.ToString();
    private static string productHeadphonesId => DataSeeder.productHeadphonesId.ToString();
    private static string productMissingId => "66666666-6666-6666-6666-666666666699";

    private static object[] CompositeKey_ProductReview => DataSeeder.ReviewLapTopKey;
    private static object[] CompositeKey_MissingReview => [DataSeeder.productLaptopId, Guid.Parse("77777777-7777-7777-7777-777777777799")];
    private static object[] CompositeKey_ProductReview_Reversed => [CompositeKey_ProductReview[1], CompositeKey_ProductReview[0]];
}
