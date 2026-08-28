using System.Net;
using System.Text.Json;
using KyrolusSous.OpenApi;
using KyrolusSous.OpenApi.SwaggerUI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Shouldly;

namespace KyrolusSous.OpenApi.IntegrationTests;

public class OpenApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
        });
    }

    [Fact(DisplayName = "Get Open Api Json V1 Returns Success And Correct Content")]
    public async Task GetOpenApiJson_V1_ReturnsSuccessAndCorrectContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("application/json");

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var info = doc.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().ShouldBe("My API - V1 (Full Test)");
        info.GetProperty("version").GetString().ShouldBe("v1");
        info.GetProperty("description").GetString().ShouldBe("This is the first version of my API.");
        info.GetProperty("termsOfService").GetString().ShouldBe("https://example.com/terms");
        info.GetProperty("contact").GetProperty("name").GetString().ShouldBe("Support Team");
        info.GetProperty("contact").GetProperty("email").GetString().ShouldBe("support@example.com");

        var components = doc.RootElement.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("Bearer", out _).ShouldBeTrue();
        securitySchemes.TryGetProperty("CustomOAuth2", out _).ShouldBeTrue();

        var paths = doc.RootElement.GetProperty("paths");
        var weatherGet = paths.GetProperty("/weatherforecast").GetProperty("get");
        var responses = weatherGet.GetProperty("responses");
        responses.TryGetProperty("400", out _).ShouldBeTrue();
        responses.TryGetProperty("500", out _).ShouldBeTrue();
    }

    [Fact(DisplayName = "Get Open Api Json V2 Returns Success And Correct Content")]
    public async Task GetOpenApiJson_V2_ReturnsSuccessAndCorrectContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v2.json");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("application/json");

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var info = doc.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().ShouldBe("My API - V2 (Full Test)");
        info.GetProperty("version").GetString().ShouldBe("v2");
        info.GetProperty("description").GetString().ShouldBe("Version 2 with new features.");
        info.GetProperty("license").GetProperty("name").GetString().ShouldBe("MIT License");
        info.GetProperty("license").GetProperty("url").GetString().ShouldBe("https://example.com/license");
    }

    [Fact(DisplayName = "Get Scalar Ui Returns Success And Html Content")]
    public async Task GetScalarUi_ReturnsSuccessAndHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("text/html");
    }

    [Fact(DisplayName = "Get Swagger Ui Returns Success And Html Content")]
    public async Task GetSwaggerUi_ReturnsSuccessAndHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/my-docs/index.html");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("text/html");
    }

    [Fact(DisplayName = "Get Re Doc Ui Returns Success And Html Content")]
    public async Task GetReDocUi_ReturnsSuccessAndHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/redoc");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("text/html");
    }

    [Fact(DisplayName = "Open Api Options Defaults Are Valid")]
    public void OpenApiOptions_Defaults_AreValid()
    {
        var options = new KyrolusOpenApiOptions();

        options.EnableJwtBearerAuth.ShouldBeTrue();
        options.JwtBearerScheme.ShouldBe("Bearer");
        options.EnableScalarUi.ShouldBeTrue();
        options.ScalarRoutePrefix.ShouldBe("scalar");
        options.EnableSwaggerUi.ShouldBeTrue();
        options.SwaggerUiRoutePrefix.ShouldBe("swagger");
        options.EnableReDocUi.ShouldBeTrue();
        options.ReDocRoutePrefix.ShouldBe("redoc");
        options.EnableSmartAutoTagging.ShouldBeTrue();
        options.EnableStandardErrorResponses.ShouldBeTrue();
        options.ScalarTheme.ShouldBe(ScalarTheme.Default);
        options.EnableApiKeyAuth.ShouldBeFalse();
        options.ApiKeyHeaderName.ShouldBe("X-Api-Key");
        options.EnableBasicAuth.ShouldBeFalse();
        options.BasicAuthSchemeName.ShouldBe("Basic");
    }

    [Fact(DisplayName = "Add Kyrolus Open Api Binds Configuration Correctly")]
    public void AddKyrolusOpenApi_BindsConfigurationCorrectly()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "KyrolusOpenApi:Title", "Custom Config API" },
            { "KyrolusOpenApi:EnableApiKeyAuth", "true" },
            { "KyrolusOpenApi:ApiKeyHeaderName", "X-Custom-Key" },
            { "KyrolusOpenApi:EnableBasicAuth", "true" },
            { "KyrolusOpenApi:EnableReDocUi", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddKyrolusOpenApi(configuration.GetSection("KyrolusOpenApi"));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<KyrolusOpenApiOptions>>().Value;

        options.Title.ShouldBe("Custom Config API");
        options.EnableApiKeyAuth.ShouldBeTrue();
        options.ApiKeyHeaderName.ShouldBe("X-Custom-Key");
        options.EnableBasicAuth.ShouldBeTrue();
        options.EnableReDocUi.ShouldBeTrue();
    }

    [Fact(DisplayName = "Allow Anonymous Endpoint Has No Security Requirements")]
    public async Task AllowAnonymousEndpoint_HasNoSecurityRequirements()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var pingGet = paths.GetProperty("/public/ping").GetProperty("get");

        // Should not have security requirement attached
        if (pingGet.TryGetProperty("security", out var sec))
        {
            sec.GetArrayLength().ShouldBe(0);
        }
    }

    [Fact(DisplayName = "Authorized Endpoint Has Security Requirements And Documents Roles")]
    public async Task AuthorizedEndpoint_HasSecurityRequirements_AndDocumentsRoles()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var adminGet = paths.GetProperty("/secure/admin").GetProperty("get");

        adminGet.TryGetProperty("security", out var sec).ShouldBeTrue();
        sec.GetArrayLength().ShouldBeGreaterThan(0);

        var desc = adminGet.GetProperty("description").GetString();
        desc.ShouldNotBeNull();
        desc.ShouldContain("Required Roles");
        desc.ShouldContain("Admin,Manager");
    }

    [Fact(DisplayName = "Operations Include Tenant Id Header When Enabled")]
    public async Task Operations_IncludeTenantIdHeader_WhenEnabled()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var weatherGet = paths.GetProperty("/weatherforecast").GetProperty("get");

        weatherGet.TryGetProperty("parameters", out var parameters).ShouldBeTrue();
        var hasTenantHeader = false;
        foreach (var param in parameters.EnumerateArray())
        {
            if (param.GetProperty("name").GetString() == "X-Tenant-Id" &&
                param.GetProperty("in").GetString() == "header")
            {
                hasTenantHeader = true;
                break;
            }
        }

        hasTenantHeader.ShouldBeTrue();
    }

    [Fact(DisplayName = "Error Responses Include Problem Details And Not Found Response")]
    public async Task ErrorResponses_IncludeProblemDetails_And_NotFoundResponse()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var weatherGet = paths.GetProperty("/weatherforecast").GetProperty("get");
        var responses = weatherGet.GetProperty("responses");

        // 400 Bad Request has ProblemDetails content
        responses.TryGetProperty("400", out var resp400).ShouldBeTrue();
        resp400.TryGetProperty("content", out var content400).ShouldBeTrue();
        content400.TryGetProperty("application/problem+json", out _).ShouldBeTrue();

        // 404 Not Found is included because IncludeNotFoundResponse = true
        responses.TryGetProperty("404", out var resp404).ShouldBeTrue();
        resp404.GetProperty("description").GetString().ShouldBe("Not Found");
    }

    [Fact(DisplayName = "Multi Version Scalar And Re Doc Endpoints Return Success")]
    public async Task MultiVersion_ScalarAndReDoc_EndpointsReturnSuccess()
    {
        var client = _factory.CreateClient();

        // Scalar individual version routes
        var scalarV1 = await client.GetAsync("/scalar/v1");
        scalarV1.EnsureSuccessStatusCode();

        var scalarV2 = await client.GetAsync("/scalar/v2");
        scalarV2.EnsureSuccessStatusCode();

        // ReDoc individual version routes
        var redocV1 = await client.GetAsync("/redoc/v1");
        redocV1.EnsureSuccessStatusCode();

        var redocV2 = await client.GetAsync("/redoc/v2");
        redocV2.EnsureSuccessStatusCode();
    }

    [Fact(DisplayName = "Configure For Open Iddict Configures O Auth2 Correctly")]
    public void ConfigureForOpenIddict_ConfiguresOAuth2Correctly()
    {
        var options = new KyrolusOpenApiOptions();
        options.ConfigureForOpenIddict();

        options.EnableOAuth2Auth.ShouldBeTrue();
        options.OAuth2SchemeName.ShouldBe("OpenIddict");
        options.OAuth2Flow.ShouldBe("authorizationcode");
        options.OAuth2AuthorizationUrl.ShouldBe("/connect/authorize");
        options.OAuth2TokenUrl.ShouldBe("/connect/token");
        options.OAuth2Scopes.ContainsKey("openid").ShouldBeTrue();
        options.OAuth2Scopes.ContainsKey("profile").ShouldBeTrue();
        options.OAuth2Scopes.ContainsKey("email").ShouldBeTrue();
        options.OAuth2Scopes.ContainsKey("offline_access").ShouldBeTrue();
    }

    [Fact(DisplayName = "Tags Are Sorted Alphabetically")]
    public async Task Tags_AreSortedAlphabetically()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        if (doc.RootElement.TryGetProperty("tags", out var tags))
        {
            var tagNames = tags.EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()!)
                .ToList();

            var sorted = tagNames.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            tagNames.ShouldBe(sorted);
        }
    }

    [Fact(DisplayName = "Add Kyrolus Swagger Ui Registers Ui Provider In Services")]
    public void AddKyrolusSwaggerUi_RegistersUiProviderInServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusSwaggerUi();

        var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetService<IKyrolusOpenApiUiProvider>();

        provider.ShouldNotBeNull();
        provider.ProviderName.ShouldBe("SwaggerUI");
    }

    [Fact(DisplayName = "Obsolete Endpoint Is Marked As Deprecated And Contains Warning")]
    public async Task ObsoleteEndpoint_IsMarkedAsDeprecated_AndContainsWarning()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var legacyGet = paths.GetProperty("/legacy/products").GetProperty("get");

        legacyGet.TryGetProperty("deprecated", out var dep).ShouldBeTrue();
        dep.GetBoolean().ShouldBeTrue();

        var desc = legacyGet.GetProperty("description").GetString();
        desc.ShouldNotBeNull();
        desc.ShouldContain("Deprecated");
        desc.ShouldContain("Use /weatherforecast instead");
    }

    [Fact(DisplayName = "Rate Limited Endpoint Documents429 Response")]
    public async Task RateLimitedEndpoint_Documents429Response()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var rateLimitedGet = paths.GetProperty("/rate-limited/items").GetProperty("get");

        var responses = rateLimitedGet.GetProperty("responses");
        responses.TryGetProperty("429", out var r429).ShouldBeTrue();
        r429.GetProperty("description").GetString()!.ShouldContain("Too Many Requests");
    }

    [Fact(DisplayName = "Configure Open Api Options Applies Custom Document Modification")]
    public async Task ConfigureOpenApiOptions_AppliesCustomDocumentModification()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var weatherGet = paths.GetProperty("/weatherforecast").GetProperty("get");
        var responses = weatherGet.GetProperty("responses");

        responses.TryGetProperty("418", out var r418).ShouldBeTrue();
        r418.GetProperty("description").GetString()!.ShouldContain("I'm a teapot (Custom Hook Applied)");
    }

    [Fact(DisplayName = "Save Open Api Document Async Writes Valid File To Disk")]
    public async Task SaveOpenApiDocumentAsync_WritesValidFileToDisk()
    {
        var client = _factory.CreateClient();
        var tempFile = Path.Combine(Path.GetTempPath(), $"openapi_test_{Guid.NewGuid():N}.json");

        try
        {
            // Use WebApplication helper
            WebApplication? dummyApp = null;
            await dummyApp!.SaveOpenApiDocumentAsync(tempFile, "v1", client);

            File.Exists(tempFile).ShouldBeTrue();
            var json = await File.ReadAllTextAsync(tempFile);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("openapi", out _).ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact(DisplayName = "Anonymous Endpoint Does Not Contain401 And403 When Smart Auth Enabled")]
    public async Task AnonymousEndpoint_DoesNotContain401And403_WhenSmartAuthEnabled()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var pingGet = paths.GetProperty("/public/ping").GetProperty("get");
        var responses = pingGet.GetProperty("responses");

        // 400 and 500 should be present, but 401 and 403 must NOT be present on anonymous routes
        responses.TryGetProperty("400", out _).ShouldBeTrue();
        responses.TryGetProperty("500", out _).ShouldBeTrue();
        responses.TryGetProperty("401", out _).ShouldBeFalse();
        responses.TryGetProperty("403", out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "Header Parameters Have Explicit String Schema Definition")]
    public async Task HeaderParameters_HaveExplicitStringSchemaDefinition()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);

        var paths = doc.RootElement.GetProperty("paths");
        var weatherGet = paths.GetProperty("/weatherforecast").GetProperty("get");

        var parameters = weatherGet.GetProperty("parameters");
        var tenantParam = parameters.EnumerateArray().FirstOrDefault(p => p.GetProperty("name").GetString() == "X-Tenant-Id");

        tenantParam.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        tenantParam.TryGetProperty("schema", out var schema).ShouldBeTrue();
        schema.GetProperty("type").GetString().ShouldBe("string");
    }

    [Fact(DisplayName = "Re Doc Endpoint Returns Valid Html With Sanitized Content")]
    public async Task ReDocEndpoint_ReturnsValidHtmlWithSanitizedContent()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/redoc");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("<redoc spec-url=");
        html.ShouldContain("Custom API Documentation");
        html.ShouldNotContain("<script>alert(");
    }
}
