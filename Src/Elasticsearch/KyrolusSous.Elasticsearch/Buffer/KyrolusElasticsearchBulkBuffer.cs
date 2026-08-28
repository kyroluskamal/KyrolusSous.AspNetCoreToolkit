using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Configuration options for the asynchronous high-throughput bulk buffer.
/// </summary>
public sealed class KyrolusElasticBulkBufferOptions
{
    /// <summary>
    /// Maximum capacity of the in-memory bounded channel buffer. Default is 50,000 items.
    /// </summary>
    public int ChannelCapacity { get; set; } = 50_000;

    /// <summary>
    /// Maximum number of documents to batch before flushing to Elasticsearch. Default is 1,000.
    /// </summary>
    public int BatchSize { get; set; } = 1_000;

    /// <summary>
    /// Maximum time duration to wait before flushing pending buffer items. Default is 2 seconds.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// High-throughput non-blocking bulk buffer for high-frequency writes (logs, metrics, IoT telemetry).
/// </summary>
public interface IKyrolusElasticsearchBulkBuffer<TDocument, TId> where TDocument : class
{
    /// <summary>
    /// Enqueues a document for background batch indexing without blocking the caller.
    /// </summary>
    ValueTask<bool> EnqueueAsync(TDocument document, TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues multiple documents for background batch indexing.
    /// </summary>
    ValueTask<int> EnqueueManyAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers an immediate flush of any buffered items to Elasticsearch.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IKyrolusElasticsearchBulkBuffer{TDocument, TId}"/> backed by System.Threading.Channels.
/// </summary>
public sealed class KyrolusElasticsearchBulkBuffer<TDocument, TId> : IKyrolusElasticsearchBulkBuffer<TDocument, TId>, IHostedService, IAsyncDisposable
    where TDocument : class, new()
{
    private readonly IKyrolusElasticRepository<TDocument, TId> _repository;
    private readonly KyrolusElasticBulkBufferOptions _options;
    private readonly ILogger<KyrolusElasticsearchBulkBuffer<TDocument, TId>>? _logger;
    private readonly Channel<(TDocument Document, TId Id)> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private Task? _processingTask;

    public KyrolusElasticsearchBulkBuffer(
        IKyrolusElasticRepository<TDocument, TId> repository,
        IOptions<KyrolusElasticBulkBufferOptions>? options = null,
        ILogger<KyrolusElasticsearchBulkBuffer<TDocument, TId>>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options?.Value ?? new KyrolusElasticBulkBufferOptions();
        _logger = logger;

        var channelOptions = new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<(TDocument, TId)>(channelOptions);
    }

    public async ValueTask<bool> EnqueueAsync(TDocument document, TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            await _channel.Writer.WriteAsync((document, id), cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to enqueue document into Elasticsearch bulk buffer.");
            return false;
        }
    }

    public async ValueTask<int> EnqueueManyAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var count = 0;
        foreach (var item in items)
        {
            if (await EnqueueAsync(item.Document, item.Id, cancellationToken))
            {
                count++;
            }
        }
        return count;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            var batch = new List<(TDocument Document, TId Id)>(_options.BatchSize);
            while (_channel.Reader.TryRead(out var item))
            {
                batch.Add(item);
                if (batch.Count >= _options.BatchSize)
                {
                    await _repository.BulkIndexAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _repository.BulkIndexAsync(batch, cancellationToken);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processingTask = Task.Run(ProcessQueueAsync, CancellationToken.None);
        _logger?.LogInformation("Started Elasticsearch bulk buffer background worker for repository index '{Index}'.", _repository.IndexName);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();

        try
        {
            await _cts.CancelAsync();
            if (_processingTask is not null)
            {
                await Task.WhenAny(_processingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        // Drain any remaining items safely
        await FlushAsync(CancellationToken.None);
        _logger?.LogInformation("Stopped Elasticsearch bulk buffer background worker for repository index '{Index}'.", _repository.IndexName);
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<(TDocument Document, TId Id)>(_options.BatchSize);
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                using var timeoutTokenSource = new CancellationTokenSource(_options.FlushInterval);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutTokenSource.Token);

                while (batch.Count < _options.BatchSize)
                {
                    try
                    {
                        if (await _channel.Reader.WaitToReadAsync(linkedCts.Token))
                        {
                            while (batch.Count < _options.BatchSize && _channel.Reader.TryRead(out var item))
                            {
                                batch.Add(item);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException) when (timeoutTokenSource.IsCancellationRequested)
                    {
                        break; // Flush interval reached
                    }
                }

                if (batch.Count > 0)
                {
                    await _flushLock.WaitAsync(token);
                    try
                    {
                        var result = await _repository.BulkIndexAsync(batch, token);
                        if (result.HasErrors)
                        {
                            _logger?.LogWarning("Elasticsearch bulk buffer indexed {Indexed}/{Total} items with {Failures} errors.",
                                result.IndexedCount, result.TotalCount, result.FailedCount);
                        }
                    }
                    finally
                    {
                        _flushLock.Release();
                    }

                    batch.Clear();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error occurred in Elasticsearch bulk buffer processor.");
                await Task.Delay(500, token); // Backoff briefly
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cts.CancelAsync();
            if (_processingTask is not null)
            {
                await _processingTask;
            }
        }
        catch
        {
            // Suppress on dispose
        }
        finally
        {
            _cts.Dispose();
            _flushLock.Dispose();
        }
    }
}
