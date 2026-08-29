using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

[Collection("MartenPipelineTestCollection")]
public sealed class OpenApiDocumentIntegrationTests(TestAppFactory factory)
{
    private static readonly string[] CandidateOpenApiRoutes =
    [
        "/openapi/v1.json",
        "/openapi/default.json",
        "/openapi/v1",
        "/openapi/default"
    ];

    [Fact(DisplayName = "OpenAPI document endpoint - resolved document contains expected core sections")]
    public async Task Openapi_document_endpoint_resolved_document_contains_expected_core_sections()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-success"));
        var (route, response, body) = await ResolveOpenApiDocumentAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Resolved route: {route}\n{body}");
        var json = JsonDocument.Parse(body);

        json.RootElement.TryGetProperty("openapi", out var openapiVersion).ShouldBeTrue(body);
        openapiVersion.GetString().ShouldNotBeNullOrWhiteSpace();

        json.RootElement.TryGetProperty("paths", out var paths).ShouldBeTrue(body);
        paths.ValueKind.ShouldBe(JsonValueKind.Object, body);
        paths.EnumerateObject().Any().ShouldBeTrue(body);
    }

    [Theory(DisplayName = "OpenAPI document endpoint - unknown document names are rejected")]
    [MemberData(nameof(UnknownDocumentCases))]
    public async Task Openapi_document_endpoint_unknown_document_names_are_rejected(string route)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-unknown"));
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.ShouldBeFalse($"{route}\n{body}");
    }

    [Theory(DisplayName = "OpenAPI document endpoint - supports common JSON accept headers")]
    [MemberData(nameof(JsonAcceptHeaderCases))]
    public async Task Openapi_document_endpoint_supports_common_json_accept_headers(string acceptHeader)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-accept"));
        var (_, response, body) = await ResolveOpenApiDocumentAsync(client, acceptHeader);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("json");
    }

    [Fact(DisplayName = "OpenAPI document endpoint - includes core menu and order paths")]
    public async Task Openapi_document_endpoint_includes_core_menu_and_order_paths()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-paths"));
        var (_, response, body) = await ResolveOpenApiDocumentAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var json = JsonDocument.Parse(body);
        var paths = json.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/menu-items", out _).ShouldBeTrue(body);
        paths.TryGetProperty("/api/orders", out _).ShouldBeTrue(body);
    }

    [Fact(DisplayName = "OpenAPI document endpoint - exposes schemas for pipeline contracts")]
    public async Task Openapi_document_endpoint_exposes_schemas_for_pipeline_contracts()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-schemas"));
        var (_, response, body) = await ResolveOpenApiDocumentAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("components", out var components).ShouldBeTrue(body);
        components.TryGetProperty("schemas", out var schemas).ShouldBeTrue(body);
        schemas.ValueKind.ShouldBe(JsonValueKind.Object, body);
        schemas.EnumerateObject().Any().ShouldBeTrue(body);
    }

    [Fact(DisplayName = "OpenAPI document endpoint - top-level tags include the MenuItems module")]
    public async Task Openapi_document_endpoint_top_level_tags_include_the_menuitems_module()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-tags"));
        var (_, response, body) = await ResolveOpenApiDocumentAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("tags", out var tags).ShouldBeTrue(body);
        tags.EnumerateArray()
            .Select(tag => tag.GetProperty("name").GetString())
            .ShouldContain("MenuItems", StringComparer.Ordinal);
    }

    [Fact(DisplayName = "OpenAPI document endpoint - shared request schemas expose batch and query contracts")]
    public async Task Openapi_document_endpoint_shared_request_schemas_expose_batch_and_query_contracts()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("openapi-doc-contract-schemas"));
        var (_, response, body) = await ResolveOpenApiDocumentAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var schemas = json.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        var batchRequestProperties = schemas
            .GetProperty("KyrolusBatchRequestOfMenuItemAndGuid")
            .GetProperty("properties");
        batchRequestProperties.TryGetProperty("operations", out _).ShouldBeTrue(body);
        batchRequestProperties.TryGetProperty("atomic", out _).ShouldBeTrue(body);
        batchRequestProperties.TryGetProperty("returnData", out _).ShouldBeTrue(body);

        var queryRequestProperties = schemas
            .GetProperty("QueryRequest")
            .GetProperty("properties");
        queryRequestProperties.TryGetProperty("filters", out _).ShouldBeTrue(body);
        queryRequestProperties.TryGetProperty("orderBy", out _).ShouldBeTrue(body);
        queryRequestProperties.TryGetProperty("includeGraph", out _).ShouldBeTrue(body);
    }

    public static IEnumerable<object[]> UnknownDocumentCases()
    {
        yield return ["/openapi/unknown.json"];
        yield return ["/openapi/v999.json"];
        yield return ["/openapi/__invalid__"];
    }

    public static IEnumerable<object[]> JsonAcceptHeaderCases()
    {
        yield return ["application/json"];
        yield return ["application/*+json"];
        yield return ["*/*"];
    }

    private static async Task<(string Route, HttpResponseMessage Response, string Body)> ResolveOpenApiDocumentAsync(
        HttpClient client,
        string? acceptHeader = null)
    {
        var attempts = new List<string>();

        foreach (var route in CandidateOpenApiRoutes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            if (!string.IsNullOrWhiteSpace(acceptHeader))
            {
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(acceptHeader));
            }

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return (route, response, body);
            }

            attempts.Add($"{route} => {(int)response.StatusCode}");
            response.Dispose();
        }

        throw new InvalidOperationException("Could not resolve OpenAPI document. Tried routes: " + string.Join(", ", attempts));
    }
}
