using System.Net;
using System.Text.Json;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class ExceptionHandlingTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public ExceptionHandlingTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Theory(DisplayName = "Exceptions - mapped exception matrix returns expected HTTP contract")]
    [MemberData(nameof(ExceptionContractCases))]
    public async Task Exception_matrix_returns_expected_http_contract(string route, HttpStatusCode expectedStatus, string expectedCode)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(expectedStatus, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("code").GetString().ShouldBe(expectedCode, body);
        json.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Exceptions - secure endpoint without auth returns 401")]
    public async Task Secure_endpoint_without_auth_returns_unauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/diagnostics/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public static IEnumerable<object[]> ExceptionContractCases()
    {
        yield return ["/api/diagnostics/exception", HttpStatusCode.BadRequest, "bad_request"];
        yield return ["/api/diagnostics/exception/not-found", HttpStatusCode.NotFound, "not_found"];
        yield return ["/api/diagnostics/exception/unauthorized", HttpStatusCode.Unauthorized, "unauthorized"];
        yield return ["/api/diagnostics/exception/framework-unauthorized", HttpStatusCode.Unauthorized, "unauthorized"];
        yield return ["/api/diagnostics/exception/timeout", HttpStatusCode.GatewayTimeout, "timeout"];
        yield return ["/api/diagnostics/exception/external", HttpStatusCode.BadGateway, "external_service_error"];
        yield return ["/api/diagnostics/exception/invalid-json", HttpStatusCode.BadRequest, "invalid_json"];
        yield return ["/api/diagnostics/exception/unknown", HttpStatusCode.InternalServerError, "internal_error"];
    }
}
