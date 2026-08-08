using System.Net;
using System.Net.Http.Json;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class ETagAndPagingContractIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact(DisplayName = "ETag handling - get by id returns ETag and If-None-Match returns 304")]
    public async Task Get_by_id_returns_etag_and_if_none_match_returns_not_modified()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-etag-not-modified"));
        var created = await CreateMenuItemAsync(client, "ETag Item", "Main", 10);

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, getBody);
        getResponse.Headers.ETag.ShouldNotBeNull();

        using var notModifiedRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/menu-items/{created.Id}");
        notModifiedRequest.Headers.TryAddWithoutValidation("If-None-Match", getResponse.Headers.ETag!.ToString());
        var notModifiedResponse = await client.SendAsync(notModifiedRequest);
        notModifiedResponse.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        notModifiedResponse.Headers.ETag.ShouldNotBeNull();
    }

    [Theory(DisplayName = "ETag handling - patch endpoint validates If-Match")]
    [InlineData("\"stale-etag-value\"", HttpStatusCode.Conflict)]
    [InlineData(null, HttpStatusCode.OK)]
    public async Task Patch_endpoint_validates_if_match(string? forcedIfMatch, HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-etag-if-match"));
        var created = await CreateMenuItemAsync(client, "ETag Patch", "Main", 12);

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var currentEtag = getResponse.Headers.ETag?.ToString();
        currentEtag.ShouldNotBeNullOrWhiteSpace();

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/menu-items/{created.Id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object> { ["Price"] = 55m })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", forcedIfMatch ?? currentEtag!);
        var patchResponse = await client.SendAsync(patchRequest);
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        patchResponse.StatusCode.ShouldBe(expectedStatus, patchBody);
    }

    [Theory(DisplayName = "ETag handling - If-None-Match accepts multi-value and weak validators")]
    [InlineData("stale,current-weak", HttpStatusCode.NotModified)]
    [InlineData("stale-only", HttpStatusCode.OK)]
    public async Task Get_by_id_if_none_match_accepts_multi_value_and_weak_validators(string mode, HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"menuitem-etag-if-none-match-{mode}"));
        var created = await CreateMenuItemAsync(client, "ETag Multi If-None-Match", "Main", 16);

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var currentEtag = getResponse.Headers.ETag?.ToString();
        currentEtag.ShouldNotBeNullOrWhiteSpace();

        var headerValue = mode switch
        {
            "stale,current-weak" => "\"stale-etag\", W/" + currentEtag,
            "stale-only" => "\"stale-etag\", \"another-stale\"",
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/menu-items/{created.Id}");
        request.Headers.TryAddWithoutValidation("If-None-Match", headerValue);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Theory(DisplayName = "ETag handling - patch endpoint accepts matching token among multi-value If-Match headers")]
    [InlineData("current-first", HttpStatusCode.OK)]
    [InlineData("weak-current-second", HttpStatusCode.OK)]
    [InlineData("all-stale", HttpStatusCode.Conflict)]
    public async Task Patch_endpoint_accepts_matching_token_among_multi_value_if_match_headers(string mode, HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"menuitem-etag-if-match-multi-{mode}"));
        var created = await CreateMenuItemAsync(client, "ETag Multi If-Match", "Main", 18);

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var currentEtag = getResponse.Headers.ETag?.ToString();
        currentEtag.ShouldNotBeNullOrWhiteSpace();

        var headerValue = mode switch
        {
            "current-first" => currentEtag!,
            "weak-current-second" => "\"stale-etag\", W/" + currentEtag,
            "all-stale" => "\"stale-etag\", \"another-stale\"",
            _ => throw new InvalidOperationException($"Unknown mode '{mode}'.")
        };

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/menu-items/{created.Id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object> { ["Price"] = 77m })
        };
        patchRequest.Headers.TryAddWithoutValidation("If-Match", headerValue);
        var patchResponse = await client.SendAsync(patchRequest);
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        patchResponse.StatusCode.ShouldBe(expectedStatus, patchBody);
    }

    [Theory(DisplayName = "IncludeGraph validation - disabled include graph returns 400")]
    [InlineData("/api/menu-items/{0}?includeGraph=Category")]
    [InlineData("/api/menu-items/by-keys?keys={0}&includeGraph=Category")]
    public async Task Include_graph_when_disabled_returns_bad_request(string pathTemplate)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-include-graph-disabled"));
        var created = await CreateMenuItemAsync(client, "Graph Disabled", "Main", 11);

        var response = await client.GetAsync(string.Format(pathTemplate, created.Id));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("IncludeGraph is not enabled.");
    }

    [Fact(DisplayName = "Paged endpoint - includeDeleted true uses fallback and includes soft-deleted")]
    public async Task Paged_include_deleted_returns_soft_deleted_items()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-paged-include-deleted"));
        var keep = await CreateMenuItemAsync(client, "Keep", "Main", 10);
        var deleted = await CreateMenuItemAsync(client, "DeleteMe", "Main", 20);
        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{deleted.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=20&includeDeleted=true");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<PagedPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.ShouldContain(x => x.Id == deleted.Id && x.IsDeleted);
        payload.Items.ShouldContain(x => x.Id == keep.Id && !x.IsDeleted);
    }

    [Fact(DisplayName = "Paged query endpoint - includeDeleted false executes standard paged query path")]
    public async Task Query_paged_without_include_deleted_executes_standard_path()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-paged-standard"));
        await CreateMenuItemAsync(client, "Main-1", "Main", 10);
        await CreateMenuItemAsync(client, "Main-2", "Main", 20);
        await CreateMenuItemAsync(client, "Drink-1", "Drinks", 30);

        var request = new TestPagedQueryRequest(
            Request: new TestQueryRequest(
                Filters: [new TestFilterClause("Category", "eq", "Main")],
                OrderBy: [new TestOrderClause("Price", Desc: false)]),
            PageNumber: 1,
            PageSize: 1,
            Cacheable: false,
            IncludeDeleted: false);

        var response = await client.PostAsJsonAsync("/api/menu-items/query/paged", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<PagedPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.Count.ShouldBe(1);
        payload.TotalCount.ShouldBe(2);
        payload.Items[0].Category.ShouldBe("Main");
    }

    [Fact(DisplayName = "Query endpoint - includeGraph array payload returns bad request when feature is disabled")]
    public async Task Query_with_include_graph_array_returns_bad_request_when_disabled()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-include-graph-array"));
        await CreateMenuItemAsync(client, "Graph Array", "Main", 10);

        var payload = new
        {
            includeGraph = new[] { "Category", "Name" }
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/query", payload);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("IncludeGraph is not enabled.");
    }

    private static async Task<MenuItem> CreateMenuItemAsync(HttpClient client, string name, string category, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = name,
            Category = category,
            Price = price
        });
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<MenuItem>();
        item.ShouldNotBeNull();
        return item!;
    }

    private sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
}

