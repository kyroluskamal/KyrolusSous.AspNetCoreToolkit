using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class AuthFlowTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory;

    public AuthFlowTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = "Auth - token grants access to secure endpoint")]
    public async Task Can_request_token_and_access_secure_endpoint()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var response = await client.GetAsync("/api/diagnostics/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Auth - secure endpoint requires bearer token")]
    public async Task Secure_endpoint_requires_authentication()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var response = await client.GetAsync("/api/diagnostics/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Auth - invalid credentials return bad request")]
    public async Task Invalid_credentials_return_bad_request()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "admin",
            ["password"] = "wrong",
            ["scope"] = "api"
        };

        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Auth - cashier token grants access to secure endpoint")]
    public async Task Cashier_token_grants_access()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("auth-cashier"));
        var token = await client.GetAccessTokenAsync("cashier", "cashier123");
        client.SetBearerToken(token);

        var response = await client.GetAsync("/api/diagnostics/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
