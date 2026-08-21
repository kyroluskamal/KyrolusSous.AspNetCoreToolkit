using System.Net;
using System.Text.Json;
using KyrolusSous.OpenApi;
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task GetScalarUi_ReturnsSuccessAndHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("text/html");
    }

    [Fact]
    public async Task GetSwaggerUi_ReturnsSuccessAndHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/my-docs/index.html");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("text/html");
    }

    [Fact]
    public async Task GetReDocUi_ReturnsSuccessAndHtmlContent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/redoc");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().ShouldContain("text/html");
    }

    [Fact]
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

    [Fact]
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
}
