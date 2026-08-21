namespace KyrolusSous.Elasticsearch;

public class ElasticsearchIndexInitializerHostedService(
    IServiceProvider serviceProvider,
    IOptions<KyrolusElasticsearchOptions> options,
    ILogger<ElasticsearchIndexInitializerHostedService>? logger = null) : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly KyrolusElasticsearchOptions _options = options.Value;
    private readonly ILogger<ElasticsearchIndexInitializerHostedService>? _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateIndices)
        {
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var indexManager = scope.ServiceProvider.GetRequiredService<IElasticIndexManager>();

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return []; }
                })
                .Where(t => t.GetCustomAttribute<ElasticIndexAttribute>() is not null);

            foreach (var type in types)
            {
                var method = typeof(IElasticIndexManager).GetMethods()
                    .FirstOrDefault(m => m.Name == nameof(IElasticIndexManager.CreateIndexAsync) && m.IsGenericMethod);

                var genericMethod = method?.MakeGenericMethod(type);
                if (genericMethod is not null)
                {
                    var task = (Task<bool>)genericMethod.Invoke(indexManager, [cancellationToken])!;
                    await task;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to auto-provision Elasticsearch indices at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
