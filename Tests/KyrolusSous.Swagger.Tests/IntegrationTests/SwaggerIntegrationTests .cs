using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using KyrolusSous.Swagger;
using Microsoft.AspNetCore.Hosting;
namespace IntegrationTests;


public class SwaggerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dummyXmlFilePath;

    public SwaggerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Create a dummy XML file for testing XML comments in integration tests.
        _dummyXmlFilePath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
        File.WriteAllText(_dummyXmlFilePath, "<doc><assembly><name>TestAssembly</name></assembly><members></members></doc>");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");

            builder.ConfigureServices(services =>
            {
                services.PostConfigure<SwaggerServiceOptions>(options =>
                {
                    if (options.EnableXmlComments)
                    {
                        options.XmlDocAbsolutePaths.Clear();
                        options.XmlDocAbsolutePaths.Add(_dummyXmlFilePath);
                    }
                });
            });
        });
    }

    // Clean up resources.
    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _factory.Dispose();
                if (File.Exists(_dummyXmlFilePath))
                {
                    File.Delete(_dummyXmlFilePath);
                }
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~SwaggerIntegrationTests()
    {
        Dispose(false);
    }

    [Fact(DisplayName = "Swagger JSON for V1 should return success and correct content")]
    public async Task GetSwaggerJson_V1_ReturnsSuccessAndCorrectContent()
    {
        // Arrange: Create an HTTP client to send requests to the in-memory test server.
        var client = _factory.CreateClient();

        // Act: Send a GET request to the Swagger JSON endpoint for V1.
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert: Check the HTTP response.
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("application/json");

        var jsonString = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonString);

        doc.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("My API - V1 (Full Test)");
        doc.RootElement.GetProperty("info").GetProperty("version").GetString().Should().Be("v1");
        doc.RootElement.GetProperty("info").GetProperty("description").GetString().Should().Be("This is the first version of my API.");
        doc.RootElement.GetProperty("info").GetProperty("termsOfService").GetString().Should().Be("https://example.com/terms");
        doc.RootElement.GetProperty("info").GetProperty("contact").GetProperty("name").GetString().Should().Be("Support Team");
        doc.RootElement.GetProperty("info").GetProperty("contact").GetProperty("email").GetString().Should().Be("support@example.com");
        doc.RootElement.GetProperty("info").GetProperty("contact").GetProperty("url").GetString().Should().Be("https://example.com/contact");
        doc.RootElement.GetProperty("paths").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Swagger JSON for V2 should return success and correct content")]
    public async Task GetSwaggerJson_V2_ReturnsSuccessAndCorrectContent()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v2/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("application/json");

        var jsonString = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonString);

        doc.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("My API - V2 (Full Test)"); // From TestProgram.cs config
        doc.RootElement.GetProperty("info").GetProperty("version").GetString().Should().Be("v2");
        doc.RootElement.GetProperty("info").GetProperty("description").GetString().Should().Be("Version 2 with new features.");
        doc.RootElement.GetProperty("info").GetProperty("license").GetProperty("name").GetString().Should().Be("MIT License");
    }

    [Fact(DisplayName = "Swagger UI should return success and HTML content with custom route and title")]
    public async Task GetSwaggerUI_ReturnsSuccessAndHtmlContentWithCustomizations()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act: Request the custom UI route
        var response = await client.GetAsync("/my-docs");
        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Contain("text/html");

        var htmlString = await response.Content.ReadAsStringAsync();

        htmlString.Should().Contain("<title>Custom API Documentation</title>");
        htmlString.Should().Contain("swagger-ui");

    }

    [Fact(DisplayName = "Swagger UI should be disabled in Production environment")]
    public async Task SwaggerUI_ShouldBeDisabledInProduction()
    {
        // Arrange: Create a factory configured for "Production" environment
        var productionFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Production");
        });
        var client = productionFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/my-docs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Swagger JSON for default v1 should return success when API versioning is explicitly disabled")]
    public async Task GetSwaggerJson_DefaultVersion_ReturnsSuccessWhenVersioningDisabled()
    {
        // Arrange: Create a new factory instance for this specific test
        var factoryForNoVersioning = new WebApplicationFactory<Program>();
        factoryForNoVersioning = factoryForNoVersioning.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.ConfigureServices(services =>
            {
                // Reconfigure SwaggerServiceOptions for this specific test
                services.PostConfigure<SwaggerServiceOptions>(options =>
                {
                    options.EnableApiVersioning = false;
                    options.ApiVersions = new List<ApiVersionInfo> { new ApiVersionInfo { Version = "v1", Title = "Single Version Test" } };

                    options.EnableJwtBearerAuth = false;
                    options.EnableOAuth2Auth = false;
                    options.EnableXmlComments = false;
                    options.UiRoutePrefix = "swagger";
                });
            });
        });
        var client = factoryForNoVersioning.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonString);

        doc.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("Single Version Test"); //
        doc.RootElement.GetProperty("info").GetProperty("version").GetString().Should().Be("v1");

        // Ensure V2 is not available when versioning is disabled
        var v2Response = await client.GetAsync("/swagger/v2/swagger.json");
        v2Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Swagger JSON should include XML comments when enabled and file exists")]
    public async Task GetSwaggerJson_ShouldIncludeXmlComments_WhenEnabledAndFileExists()
    {
        // Arrange: Create a new factory for this specific test
        var factoryWithXml = new WebApplicationFactory<Program>();
        factoryWithXml = factoryWithXml.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<SwaggerServiceOptions>(options =>
                {
                    options.EnableXmlComments = true;
                    options.XmlDocAbsolutePaths.Clear();
                    options.XmlCommentAssemblies.Clear();
                    options.XmlDocAbsolutePaths.Add(_dummyXmlFilePath);
                });
            });
        });
        var client = factoryWithXml.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonString);

        doc.RootElement.GetProperty("paths").GetProperty("/weatherforecast").EnumerateObject().Should().NotBeEmpty();
        doc.RootElement.GetProperty("paths").GetProperty("/weatherforecast").GetProperty("get").GetProperty("operationId").GetString().Should().Be("GetWeatherForecast");
    }
}

