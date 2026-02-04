using System.Net.Http.Headers;
using System.Text.Json;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public static class TestHelpers
{
    public static string NewTenantId(string prefix)
        => $"tenant-{prefix}-{Guid.NewGuid():N}";

    public static HttpClient CreateClientWithTenant(this TestAppFactory factory, string tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return client;
    }

    public static async Task<string> GetAccessTokenAsync(this HttpClient client, string username, string password)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "api"
        };

        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
