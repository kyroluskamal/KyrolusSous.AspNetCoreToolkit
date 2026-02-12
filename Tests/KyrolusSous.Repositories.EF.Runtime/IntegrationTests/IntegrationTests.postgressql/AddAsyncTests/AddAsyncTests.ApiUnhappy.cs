namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddAsyncTests;

public partial class AddAsyncTests
{
    private sealed record InvalidAddApiSpec(
        string Route,
        Func<string> BuildPayload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidAddApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();

    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "AddAsync API rejects invalid payloads")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task AddAsync_Api_InvalidPayload_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PostRawAsync($"/api/{spec.Route}", spec.BuildPayload());

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static Dictionary<string, InvalidAddApiSpec> BuildInvalidApiSpecs()
        => new()

        {
            ["product-duplicate-id"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(
                    CreateValidProduct(
                        id: DataSeeder.productLaptopId,
                        sku: $"DUP-{Guid.NewGuid():N}",
                        name: "Duplicate Product Id"),
                    JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-duplicate-unique-sku"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(
                    CreateValidProduct(
                        id: Guid.NewGuid(),
                        storeId: DataSeeder.storeId,
                        sku: "LP-15",
                        name: "Duplicate Product SKU"),
                    JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-invalid-store"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(
                    CreateValidProduct(
                        storeId: Guid.NewGuid(),
                        name: "Invalid FK Product"),
                    JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-null-name"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new
                {
                    Id = Guid.NewGuid(),
                    StoreId = DataSeeder.storeId,
                    Name = (string?)null,
                    Sku = $"SKU-{Guid.NewGuid():N}",
                    Price = 10m,
                    AddedIn = new DateOnly(2026, 1, 1),
                    AddedAt = (TimeOnly?)null,
                    FinishedAt = TimeSpan.FromHours(1),
                    DiscontinuedAt = (DateTime?)null,
                    StockQuantity = 1,
                    Weight = (decimal?)null,
                    Count = (int?)null,
                    IsActive = true,
                    RowVersion = new byte[] { 0 },
                    IsDeleted = false,
                    DeletedAt = (DateTimeOffset?)null
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-duplicate-composite-key"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(
                    CreateValidReview(
                        productId: DataSeeder.productLaptopId,
                        customerId: DataSeeder.customerJaneId,
                        rating: 1,
                        comment: "Duplicate review"),
                    JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-invalid-product"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(
                    CreateValidReview(
                        productId: Guid.NewGuid(),
                        customerId: DataSeeder.customerJohnId,
                        rating: 3),
                    JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["malformed-json"] = new(
                Route: "product",
                BuildPayload: static () => "{ this is not valid json }",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}

