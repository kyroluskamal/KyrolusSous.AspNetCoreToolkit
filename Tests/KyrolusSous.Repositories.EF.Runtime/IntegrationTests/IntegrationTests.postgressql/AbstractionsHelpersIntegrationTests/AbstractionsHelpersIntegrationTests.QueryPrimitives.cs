namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AbstractionsHelpersIntegrationTests;

public sealed class AbstractionsHelpersIntegrationTests_QueryPrimitives(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, string, int> ApiValidRequestCases => new()
    {
        { "blank-request", " ", 3 },
        { "plain-json-filter", "{\"filters\":[{\"property\":\"Name\",\"operator\":\"eq\",\"value\":\"Laptop Pro 15\"}]}", 1 },
        { "urlencoded-json-filter", WebUtility.UrlEncode("{\"filters\":[{\"property\":\"Name\",\"operator\":\"eq\",\"value\":\"Clean Code\"}],\"asNoTracking\":true}"), 1 }
    };

    public static TheoryData<string, string> ApiInvalidRequestCases => new()
    {
        { "invalid-json-shape", "{invalid-json" },
        { "invalid-boolean-type", "{\"asNoTracking\":\"not-a-bool\"}" }
    };

    [Theory(DisplayName = "Products API accepts valid QueryRequest payloads through request query parameter")]
    [MemberData(nameof(ApiValidRequestCases))]
    public async Task ProductsApi_ValidRequestPayloads_ReturnExpectedCounts(string caseId, string payload, int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var response = await _client.GetAsync($"/api/product?request={Uri.EscapeDataString(payload)}");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var products = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        products.ShouldNotBeNull();
        products.Count.ShouldBe(expectedCount);
    }

    [Theory(DisplayName = "Products API rejects invalid QueryRequest payloads through request query parameter")]
    [MemberData(nameof(ApiInvalidRequestCases))]
    public async Task ProductsApi_InvalidRequestPayloads_ReturnBadRequest(string caseId, string payload)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var response = await _client.GetAsync($"/api/product?request={Uri.EscapeDataString(payload)}");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        content.ShouldNotBeNullOrWhiteSpace();
    }
}
