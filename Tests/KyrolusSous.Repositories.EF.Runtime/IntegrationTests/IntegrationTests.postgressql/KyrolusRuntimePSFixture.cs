namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql;


public class KyrolusRuntimePSFixture(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    public HttpClient _client = default!;
    private WebApplicationFactory<Program> _factory = default!;
    public WebApplicationFactory<Program> Factory => _factory;

    public static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task InitializeAsync()
    {
        _factory = WithPolicy();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
    public WebApplicationFactory<Program> WithPolicy(KyrolusRepositoryPolicy? policy = null)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<CommandCounterInterceptor>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<CommandCounterInterceptor>();
                    options.UseNpgsql("Host=localhost;Port=5432;Database=kyrolus_runtime_tests;Username=postgres;Password=postgres");
                    options.AddInterceptors(interceptor);
                });
                services.AddKyrolusRuntimeRepositories();
                services.AddKyrolusRuntimeDefaults<ApplicationDbContext>();
                services.RemoveAll<KyrolusRepositoryPolicy>();
                if (policy is not null)
                    services.AddSingleton(policy ?? KyrolusRepositoryPolicy.Default);
            });
        });

    public async Task<(HttpResponseMessage response, List<TEntity>? items, string? content)> ArrangeAndActUseingHttpForListAsync<TEntity>(QueryRequest? queyrequest = null)
    {
        var withRequest = queyrequest is not null ? $"?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}" : string.Empty;
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/{typeof(TEntity).Name.ToLower()}{withRequest}");
        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        List<TEntity>? items = null;
        if (response.IsSuccessStatusCode)
            items = JsonSerializer.Deserialize<List<TEntity>>(content, JsonOptions);
        return (response, items, content);
    }
    public async Task<(HttpResponseMessage response, TEntity? item, string? content)> ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<TEntity, TKey>(TKey id, QueryRequest? queyrequest = null)
    {
        var withRequest = queyrequest is not null ? $"?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}" : string.Empty;
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/{typeof(TEntity).Name.ToLower()}/{id}{withRequest}");
        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        TEntity? item = default;
        if (response.IsSuccessStatusCode)
            item = JsonSerializer.Deserialize<TEntity>(content, JsonOptions);
        return (response, item, content);
    }
    public async Task<(HttpResponseMessage response, TEntity? item, string? content)>
    ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<TEntity>(
        object?[] keyValues,
        QueryRequest? queyrequest = null)
    {
        var basePath = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/by-id";
        var keysQuery = string.Join("&", keyValues.Select(kv => $"keys={Uri.EscapeDataString(kv?.ToString() ?? "")}"));
        var url = $"{basePath}?{keysQuery}";
        if (queyrequest is not null)
        {
            var reqJson = Uri.EscapeDataString(JsonSerializer.Serialize(queyrequest, JsonOptions));
            url = $"{basePath}?request={reqJson}&{keysQuery}";
        }
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        TEntity? item = default;
        if (response.IsSuccessStatusCode)
            item = JsonSerializer.Deserialize<TEntity>(content, JsonOptions);
        return (response, item, content);
    }

}
