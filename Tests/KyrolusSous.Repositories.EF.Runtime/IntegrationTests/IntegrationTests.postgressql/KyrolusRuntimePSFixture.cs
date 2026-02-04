using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql;


public class KyrolusRuntimePSFixture(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    public HttpClient _client = default!;
    private WebApplicationFactory<Program> _factory = default!;
    private readonly string _databaseName = $"kyrolus_runtime_tests_{Guid.NewGuid():N}";
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
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            });
            var connectionString = $"Host=localhost;Port=5432;Database={_databaseName};Username=postgres;Password=postgres";
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connectionString
                };
                config.AddInMemoryCollection(overrides);
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<CommandCounterInterceptor>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<CommandCounterInterceptor>();
                    options.UseNpgsql(connectionString);
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
    public async Task WithSoftDeletedAsync_SingleKey<TEntity>(
    Guid id,
    Func<HttpResponseMessage, List<TEntity>?, string?, KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, TEntity, Guid>, Task> testBody, QueryRequest? queyrequest = null)
    where TEntity : class
    {
        var qRequest = queyrequest is null ? new QueryRequest { IncludeDeleted = true } : queyrequest with { IncludeDeleted = true };
        await using var scope = Factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, TEntity, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        bool deleted = false;
        try
        {
            deleted = await repo.SoftDeleteAsync(id);
            deleted.ShouldBeTrue();
            var result = await uow.SaveChangesAsync();
            result.ShouldBeGreaterThan(0);
            var (response, items, content) = await ArrangeAndActUseingHttpForListAsync<TEntity>(qRequest);
            await testBody(response, items!, content, repo);
        }
        finally
        {
            if (deleted)
            {
                await repo.RestoreAsync(id);
                var result = await uow.SaveChangesAsync();
                result.ShouldBeGreaterThan(0);
            }
        }
    }
    public async Task WithSoftDeletedAsync_CompositeKey<TEntity>(
    object[] keyValues,
    Func<HttpResponseMessage, List<TEntity>?, string?, KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, TEntity>, Task> testBody, QueryRequest? queyrequest = null)
    where TEntity : class
    {
        var qRequest = queyrequest is null ? new QueryRequest { IncludeDeleted = true } : queyrequest with { IncludeDeleted = true };
        await using var scope = Factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, TEntity>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        bool deleted = false;
        try
        {
            deleted = await repo.SoftDeleteAsync(keyValues);
            deleted.ShouldBeTrue();
            var result = await uow.SaveChangesAsync();
            result.ShouldBeGreaterThan(0);
            var (response, items, content) = await ArrangeAndActUseingHttpForListAsync<TEntity>(qRequest);
            await testBody(response, items!, content, repo);
        }
        finally
        {
            if (deleted)
            {
                await repo.RestoreAsync(keyValues);
                var result = await uow.SaveChangesAsync();
                result.ShouldBeGreaterThan(0);
            }
        }
    }
}
