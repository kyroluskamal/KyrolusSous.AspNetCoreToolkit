
using KyrolusSous.Repositories.EF.Abstractions.Policy;

namespace KyrolusSous.Repositories.EF.Generator.IntegrationTests;

public class KyrolusGeneratorFixture(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
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
            { // 1) interceptor singleton
                services.AddSingleton<CommandCounterInterceptor>();

                // 2) remove existing DbContext registrations (so we can re-add with interceptors)
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();

                // 3) re-add DbContext using the SAME sqlite in-memory connection from Program
                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    var conn = sp.GetRequiredService<SqliteConnection>();
                    var counter = sp.GetRequiredService<CommandCounterInterceptor>();

                    options.UseSqlite(conn);
                    options.AddInterceptors(counter);
                });

                services.RemoveAll<KyrolusRepositoryPolicy>();
                if (policy is not null)
                    services.AddSingleton(policy ?? KyrolusRepositoryPolicy.Default);
            });
        });
}
