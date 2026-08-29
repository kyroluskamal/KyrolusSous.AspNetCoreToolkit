using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

[Collection("MartenPipelineTestCollection")]
public sealed class RoutingHelperDiagnosticsIntegrationTests(TestAppFactory factory)
{
    [Theory(DisplayName = "Routing helper diagnostics - string input handles allowlist and strict mode")]
    [MemberData(nameof(StringInputCases))]
    public async Task Routing_helper_string_input_handles_allowlist_and_strict_mode(
        string? includedProperties,
        string[]? allowlist,
        bool strict,
        HttpStatusCode expectedStatus,
        string[] expectedIncludes,
        string? expectedError)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("routing-helper-string"));

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/routing-helper", new
        {
            includedProperties,
            allowlist,
            strict
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(expectedStatus, body);
        if (expectedStatus == HttpStatusCode.OK)
        {
            ReadIncluded(body).ShouldBe(expectedIncludes);
        }

        if (!string.IsNullOrWhiteSpace(expectedError))
        {
            body.ShouldContain(expectedError);
        }
    }

    [Theory(DisplayName = "Routing helper diagnostics - array input filters blanks and applies allowlist")]
    [MemberData(nameof(ArrayInputCases))]
    public async Task Routing_helper_array_input_filters_blanks_and_applies_allowlist(
        string[] includeProperties,
        string[]? allowlist,
        bool strict,
        HttpStatusCode expectedStatus,
        string[] expectedIncludes,
        string? expectedError)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("routing-helper-array"));

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/routing-helper", new
        {
            includeProperties,
            allowlist,
            strict
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(expectedStatus, body);
        if (expectedStatus == HttpStatusCode.OK)
        {
            ReadIncluded(body).ShouldBe(expectedIncludes);
        }

        if (!string.IsNullOrWhiteSpace(expectedError))
        {
            body.ShouldContain(expectedError);
        }
    }

    public static IEnumerable<object[]> StringInputCases()
    {
        yield return [null!, null!, false, HttpStatusCode.OK, Array.Empty<string>(), null!];
        yield return ["Name,Category", new[] { "Name" }, false, HttpStatusCode.OK, new[] { "Name" }, null!];
        yield return ["Name,Category", new[] { "Name" }, true, HttpStatusCode.BadRequest, Array.Empty<string>(), "Include 'Category' is not allowed."];
        yield return ["name,Category", new[] { "Name", "Category" }, true, HttpStatusCode.OK, new[] { "name", "Category" }, null!];
    }

    public static IEnumerable<object[]> ArrayInputCases()
    {
        yield return [new[] { string.Empty, "Name", "Category", " " }, null!, false, HttpStatusCode.OK, new[] { "Name", "Category" }, null!];
        yield return [new[] { "Name", "Secret" }, new[] { "Name" }, true, HttpStatusCode.BadRequest, Array.Empty<string>(), "Include 'Secret' is not allowed."];
        yield return [new[] { "name" }, new[] { "Name" }, true, HttpStatusCode.OK, new[] { "name" }, null!];
    }

    private static string[] ReadIncluded(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("included", out var includedElement) || includedElement.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return includedElement
            .EnumerateArray()
            .Select(static e => e.GetString())
            .Where(static x => !string.IsNullOrEmpty(x))
            .Select(static x => x!)
            .ToArray();
    }
}
