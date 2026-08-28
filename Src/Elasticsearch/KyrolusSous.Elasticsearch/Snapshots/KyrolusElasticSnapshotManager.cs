using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Snapshot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Snapshot status information.
/// </summary>
public sealed record KyrolusSnapshotInfo(
    string Snapshot,
    string? State,
    IReadOnlyList<string> Indices,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    long DurationInMilliseconds,
    IReadOnlyList<string>? Failures);

/// <summary>
/// Manager for Elasticsearch snapshots, backups, and index restoration.
/// </summary>
public interface IKyrolusElasticSnapshotManager
{
    /// <summary>
    /// Creates a snapshot of specified indices or all indices.
    /// </summary>
    Task<bool> CreateSnapshotAsync(string repositoryName, string snapshotName, IEnumerable<string>? indices = null, bool includeGlobalState = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a snapshot with optional index filtering and renaming patterns.
    /// </summary>
    Task<bool> RestoreSnapshotAsync(string repositoryName, string snapshotName, IEnumerable<string>? indices = null, string? renamePattern = null, string? renameReplacement = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot from a repository.
    /// </summary>
    Task<bool> DeleteSnapshotAsync(string repositoryName, string snapshotName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information and state for a snapshot.
    /// </summary>
    Task<KyrolusSnapshotInfo?> GetSnapshotStatusAsync(string repositoryName, string snapshotName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IKyrolusElasticSnapshotManager"/>.
/// </summary>
public class KyrolusElasticSnapshotManager(
    ElasticsearchClient client,
    IOptions<KyrolusElasticsearchOptions>? options = null,
    ILogger<KyrolusElasticSnapshotManager>? logger = null) : IKyrolusElasticSnapshotManager
{
    private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly KyrolusElasticsearchOptions _options = options?.Value ?? new KyrolusElasticsearchOptions();
    private readonly ILogger<KyrolusElasticSnapshotManager>? _logger = logger;

    public async Task<bool> CreateSnapshotAsync(
        string repositoryName,
        string snapshotName,
        IEnumerable<string>? indices = null,
        bool includeGlobalState = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var response = await _client.Snapshot.CreateAsync(repositoryName, snapshotName, descriptor =>
        {
            descriptor.IncludeGlobalState(includeGlobalState);
            if (indices is not null)
            {
                var indexList = indices.Select(FormatIndexName).ToList();
                if (indexList.Count > 0)
                {
                    descriptor.Indices(string.Join(",", indexList));
                }
            }
        }, cancellationToken);

        if (response.IsValidResponse)
        {
            _logger?.LogInformation("Successfully initiated snapshot '{SnapshotName}' in repository '{RepositoryName}'.", snapshotName, repositoryName);
            return true;
        }

        _logger?.LogError("Failed to create snapshot '{SnapshotName}': {Error}", snapshotName, response.DebugInformation);
        return false;
    }

    public async Task<bool> RestoreSnapshotAsync(
        string repositoryName,
        string snapshotName,
        IEnumerable<string>? indices = null,
        string? renamePattern = null,
        string? renameReplacement = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var response = await _client.Snapshot.RestoreAsync(repositoryName, snapshotName, descriptor =>
        {
            if (indices is not null)
            {
                var indexList = indices.Select(FormatIndexName).ToList();
                if (indexList.Count > 0)
                {
                    descriptor.Indices(string.Join(",", indexList));
                }
            }

            if (!string.IsNullOrWhiteSpace(renamePattern) && !string.IsNullOrWhiteSpace(renameReplacement))
            {
                descriptor.RenamePattern(renamePattern);
                descriptor.RenameReplacement(renameReplacement);
            }
        }, cancellationToken);

        if (response.IsValidResponse)
        {
            _logger?.LogInformation("Successfully restored snapshot '{SnapshotName}' from repository '{RepositoryName}'.", snapshotName, repositoryName);
            return true;
        }

        _logger?.LogError("Failed to restore snapshot '{SnapshotName}': {Error}", snapshotName, response.DebugInformation);
        return false;
    }

    public async Task<bool> DeleteSnapshotAsync(string repositoryName, string snapshotName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var response = await _client.Snapshot.DeleteAsync(repositoryName, snapshotName, cancellationToken);
        if (response.IsValidResponse)
        {
            _logger?.LogInformation("Successfully deleted snapshot '{SnapshotName}' from repository '{RepositoryName}'.", snapshotName, repositoryName);
            return true;
        }

        _logger?.LogError("Failed to delete snapshot '{SnapshotName}': {Error}", snapshotName, response.DebugInformation);
        return false;
    }

    public async Task<KyrolusSnapshotInfo?> GetSnapshotStatusAsync(string repositoryName, string snapshotName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var response = await _client.Snapshot.GetAsync(repositoryName, snapshotName, cancellationToken);
        if (!response.IsValidResponse || response.Snapshots is null || response.Snapshots.Count == 0)
        {
            return null;
        }

        var snap = response.Snapshots.FirstOrDefault();
        if (snap is null) return null;

        return new KyrolusSnapshotInfo(
            Snapshot: snap.Snapshot.ToString(),
            State: snap.State,
            Indices: snap.Indices?.Select(i => i.ToString()).ToList() ?? [],
            StartTime: snap.StartTime,
            EndTime: snap.EndTime,
            DurationInMilliseconds: snap.DurationInMillis ?? 0,
            Failures: snap.Failures?.Where(f => f is not null).Select(f => f.ToString()!).ToList()
        );
    }

    private string FormatIndexName(string rawName)
    {
        var prefix = _options.IndexPrefix ?? string.Empty;
        var suffix = _options.IndexSuffix ?? string.Empty;
        return $"{prefix}{rawName}{suffix}".ToLowerInvariant();
    }
}
