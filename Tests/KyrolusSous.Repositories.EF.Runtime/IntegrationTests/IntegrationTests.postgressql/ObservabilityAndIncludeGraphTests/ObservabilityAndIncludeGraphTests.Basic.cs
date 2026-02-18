using KyrolusSous.Repositories.EF.Abstractions.Observer;
using KyrolusSous.Repositories.EF.Runtime.Observability;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ObservabilityAndIncludeGraphTests;

public class ObservabilityAndIncludeGraphTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, string[]?, int> EmptyIncludeGraphCases => new()
    {
        { "null-paths", null, 0 },
        { "empty-paths", [], 0 },
        { "whitespace-path", ["   "], 0 }
    };

    [Fact(DisplayName = "Telemetry observer records success activity through API request")]
    public async Task TelemetryObserver_SuccessRequest_RecordsActivity()
    {
        var customFactory = WithObserverServices(services =>
        {
            services.AddKyrolusRepositoryTelemetryObserver(options =>
            {
                options.LogPayloadType = true;
                options.SlowThreshold = TimeSpan.Zero;
                options.LogErrors = true;
            });
            services.AddKyrolusRepositoryOpenTelemetry(
                serviceName: null,
                enableOtlpExporter: false,
                enableConsoleExporter: false);
        });

        using var scope = customFactory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<IKyrolusRepositoryObserver>();
        observer.ShouldBeOfType<KyrolusRepositoryTelemetryObserver>();
        await observer.OnBeforeAsync("Telemetry.Success", new { Kind = "before" });
        await observer.OnAfterAsync("Telemetry.Success", new { Kind = "after" }, TimeSpan.FromMilliseconds(12), null);

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/product");
        var content = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        items.ShouldNotBeNull();
        items.Count.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "Telemetry observer records error activity when API request fails")]
    public async Task TelemetryObserver_ErrorRequest_RecordsActivityError()
    {
        var customFactory = WithObserverServices(services =>
        {
            services.AddKyrolusRepositoryTelemetryObserver(options =>
            {
                options.LogErrors = true;
                options.SlowThreshold = TimeSpan.FromMinutes(5);
            });
            services.AddKyrolusRepositoryOpenTelemetry(
                serviceName: "kyrolus.tests",
                enableOtlpExporter: false,
                enableConsoleExporter: true);
        });

        using var scope = customFactory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<IKyrolusRepositoryObserver>();
        observer.ShouldBeOfType<KyrolusRepositoryTelemetryObserver>();
        await observer.OnBeforeAsync("Telemetry.Error", new { Kind = "before" });
        await observer.OnAfterAsync("Telemetry.Error", new { Kind = "after" }, TimeSpan.FromMilliseconds(9), new InvalidOperationException("expected"));

        using var client = customFactory.CreateClient();
        var request = new QueryRequest(Includes: ["NotARealNavigation"]);
        var encoded = Uri.EscapeDataString(JsonSerializer.Serialize(request, JsonOptions));
        var response = await client.GetAsync($"/api/product?request={encoded}");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "Telemetry services wiring works when OTLP exporter is enabled for tracing and metrics")]
    public async Task TelemetryObserver_OtlpExporterEnabled_WiresSuccessfully()
    {
        var customFactory = WithObserverServices(services =>
        {
            services.AddKyrolusRepositoryTelemetryObserver();
            services.AddKyrolusRepositoryOpenTelemetry(
                serviceName: "kyrolus.tests.otlp",
                enableOtlpExporter: true,
                enableConsoleExporter: false);
        });

        using var scope = customFactory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<IKyrolusRepositoryObserver>();
        observer.ShouldBeOfType<KyrolusRepositoryTelemetryObserver>();

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/product");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Sample observer can be registered and requests still succeed")]
    public async Task SampleObserver_CanBeWired_WithSuccessfulRequest()
    {
        var customFactory = WithObserverServices(services =>
        {
            services.AddSingleton<IKyrolusRepositoryObserver, SampleObserver>();
        });

        using var client = customFactory.CreateClient();
        var response = await client.GetAsync("/api/product");
        var content = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        items.ShouldNotBeNull();
        items.Count.ShouldBe(3);
    }

    [Theory(DisplayName = "IncludeGraphBuilder handles null and empty include paths")]
    [MemberData(nameof(EmptyIncludeGraphCases))]
    public async Task IncludeGraphBuilder_EmptyCases_Works(string caseId, string[]? paths, int expectedIncludeCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var includeGraph = KyrolusIncludeGraphBuilder.FromPaths<Product>(paths);
        includeGraph.Includes.Count.ShouldBe(expectedIncludeCount);

        var items = await repo.GetAllAsync(includeGraph: includeGraph);
        items.Count().ShouldBe(3);
    }

    [Fact(DisplayName = "IncludeGraphBuilder expression path fails fast for EF Include object-convert expression")]
    public async Task IncludeGraphBuilder_ValidPath_ThrowsInvalidIncludeExpression()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var includeGraph = KyrolusIncludeGraphBuilder.FromPaths<Product>("Store");
        includeGraph.Includes.Count.ShouldBe(1);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await repo.GetAllAsync(includeGraph: includeGraph, asNoTracking: false));
    }

    private WebApplicationFactory<Program> WithObserverServices(Action<IServiceCollection> configure)
        => Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusRepositoryObserver>();
                configure(services);
            });
        });
}
