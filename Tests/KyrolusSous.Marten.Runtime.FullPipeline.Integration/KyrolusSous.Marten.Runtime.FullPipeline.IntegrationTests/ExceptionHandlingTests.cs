using System.Net;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class ExceptionHandlingTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public ExceptionHandlingTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = "Exceptions - bad request exception mapped to 400")]
    public async Task Bad_request_exception_is_mapped()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/diagnostics/exception");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Exceptions - secure endpoint without auth returns 401")]
    public async Task Secure_endpoint_without_auth_returns_unauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/diagnostics/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
