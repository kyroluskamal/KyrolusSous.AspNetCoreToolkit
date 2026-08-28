using Microsoft.Extensions.Hosting;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Background hosted service that automatically scans loaded assemblies and initializes Elasticsearch indices and mappings on startup.
/// </summary>
public sealed class KyrolusElasticsearchIndexInitializerHostedService(
    IServiceProvider serviceProvider,
    ILogger<KyrolusElasticsearchIndexInitializerHostedService>? logger = null) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<KyrolusElasticsearchIndexInitializerHostedService>? _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting automated Elasticsearch index initialization...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var indexManager = scope.ServiceProvider.GetRequiredService<IKyrolusElasticIndexManager>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var documentTypes = assemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return []; }
                })
                .Where(t => t.IsClass && !t.IsAbstract &&
                            t.GetCustomAttribute<KyrolusElasticIndexAttribute>() is not null)
                .ToList();

            foreach (var type in documentTypes)
            {
                var method = typeof(IKyrolusElasticIndexManager)
                    .GetMethod(nameof(IKyrolusElasticIndexManager.CreateIndexAsync), [typeof(CancellationToken)])
                    ?.MakeGenericMethod(type);

                if (method is not null)
                {
                    var task = (Task<bool>)method.Invoke(indexManager, [cancellationToken])!;
                    await task;
                }
            }

            _logger?.LogInformation("Completed Elasticsearch index initialization for {Count} document types.", documentTypes.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred during Elasticsearch index auto-initialization.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
