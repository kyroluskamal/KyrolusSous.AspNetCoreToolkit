namespace KyrolusSous.Repositories.EF.Generator.IntegrationTests;

public class KyrolusGeneratorFixture(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    public HttpClient _client = default!;
    private WebApplicationFactory<Program> _factory = default!;
    public static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task InitializeAsync()
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");

            builder.ConfigureServices(services =>
            { });
        });
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

}
