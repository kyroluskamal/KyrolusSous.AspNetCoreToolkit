using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class DataProtectionTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{

    [Fact(DisplayName = "DataProtection - tenant protection roundtrip")]
    public async Task Tenant_specific_protection_roundtrips()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var response = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("secret"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProtectResponse>();
        payload.ShouldNotBeNull();
        payload!.Unprotected.ShouldBe("secret");
        payload.Protected.ShouldNotBe(payload.Unprotected);
    }

    [Fact(DisplayName = "DataProtection - protect endpoint requires authentication")]
    public async Task Protect_endpoint_requires_authentication()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var response = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("secret"));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "DataProtection - different tenants yield different ciphertext")]
    public async Task Different_tenants_get_different_protection()
    {
        using var clientA = factory.CreateClientWithTenant("tenant-alpha");
        using var clientB = factory.CreateClientWithTenant("tenant-beta");
        var tokenA = await clientA.GetAccessTokenAsync("admin", "admin123");
        var tokenB = await clientB.GetAccessTokenAsync("cashier", "cashier123");
        clientA.SetBearerToken(tokenA);
        clientB.SetBearerToken(tokenB);

        var responseA = await clientA.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("shared"));
        var responseB = await clientB.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("shared"));
        responseA.EnsureSuccessStatusCode();
        responseB.EnsureSuccessStatusCode();

        var payloadA = await responseA.Content.ReadFromJsonAsync<ProtectResponse>();
        var payloadB = await responseB.Content.ReadFromJsonAsync<ProtectResponse>();
        payloadA.ShouldNotBeNull();
        payloadB.ShouldNotBeNull();
        payloadA!.Protected.ShouldNotBe(payloadB!.Protected);
    }

    [Fact(DisplayName = "DataProtection - tenant beta roundtrip works")]
    public async Task Tenant_beta_roundtrip_works()
    {
        using var client = factory.CreateClientWithTenant("tenant-beta");
        var token = await client.GetAccessTokenAsync("cashier", "cashier123");
        client.SetBearerToken(token);

        var response = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("beta-secret"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProtectResponse>();
        payload.ShouldNotBeNull();
        payload!.Unprotected.ShouldBe("beta-secret");
    }

    [Fact(DisplayName = "DataProtection - missing tenant header uses default")]
    public async Task Missing_tenant_header_uses_default()
    {
        using var client = factory.CreateClient();
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var response = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("default-secret"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProtectResponse>();
        payload.ShouldNotBeNull();
        payload!.TenantId.ShouldBe("default");
    }
}
