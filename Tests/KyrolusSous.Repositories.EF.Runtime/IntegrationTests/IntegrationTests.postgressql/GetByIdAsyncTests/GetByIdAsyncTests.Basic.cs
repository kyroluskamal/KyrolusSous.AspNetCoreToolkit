namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private static readonly string productLaptopId = "66666666-6666-6666-6666-666666666661";
    private static readonly string productHeadphonesId = "66666666-6666-6666-6666-666666666662";
    private static readonly string[] CompositeKey_ProductReview = [productLaptopId, "77777777-7777-7777-7777-777777777772"];

    [Fact(DisplayName = "GetByIdAsync returns entity without Include Properties and with single key")]
    public async Task GetByIdAsync_ReturnsEntity_NoInclude_SingleKey()
    {
        var (response, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(productHeadphonesId);
        response.EnsureSuccessStatusCode();
        product.ShouldNotBeNull();
        product.Name.ShouldBe("Noise Cancelling Headphones");
    }

    [Fact(DisplayName = "GetByIdAsync returns entity without Include Properties and with composite key")]
    public async Task GetByIdAsync_ReturnsEntity_NoInclude_CompositeKey()
    {
        var (response, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview);
        response.EnsureSuccessStatusCode();
        review.ShouldNotBeNull();
        review.Rating.ShouldBe(5);
    }

    [Fact(DisplayName = "GetByIdAsync returns 404 for missing single key")]
    public async Task GetByIdAsync_NotFound_SingleKey()
    {
        var (response, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>("66666666-6666-6666-6666-666666666699");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        product.ShouldBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync returns 404 for missing composite key")]
    public async Task GetByIdAsync_NotFound_CompositeKey()
    {
        var missingKeys = new[] { productLaptopId, "77777777-7777-7777-7777-777777777799" };
        var (response, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(missingKeys);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        review.ShouldBeNull();
    }
}
