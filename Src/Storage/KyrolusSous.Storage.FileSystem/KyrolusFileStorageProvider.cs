using KyrolusSous.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Storage.FileSystem;

public sealed class KyrolusFileStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "KyrolusStorage");
}

public sealed class KyrolusFileStorageProvider : IKyrolusStorageProvider
{
    private readonly string _rootPath;

    public KyrolusFileStorageProvider(IOptions<KyrolusFileStorageOptions> options)
    {
        _rootPath = options?.Value?.RootPath ?? Path.Combine(AppContext.BaseDirectory, "KyrolusStorage");
        Directory.CreateDirectory(_rootPath);
    }

    public KyrolusFileStorageProvider(string rootPath)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath) ? Path.Combine(AppContext.BaseDirectory, "KyrolusStorage") : rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<KyrolusBlobProperties> UploadAsync(
        string containerName,
        string blobName,
        Stream contentStream,
        KyrolusBlobDescriptor? descriptor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentNullException.ThrowIfNull(contentStream);

        var containerDir = Path.Combine(_rootPath, SanitizePath(containerName));
        Directory.CreateDirectory(containerDir);

        var filePath = Path.Combine(containerDir, SanitizePath(blobName));
        var dirName = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dirName))
        {
            Directory.CreateDirectory(dirName);
        }

        var tempPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");
        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, overwrite: true);

        var fileInfo = new FileInfo(filePath);
        return new KyrolusBlobProperties
        {
            ContainerName = containerName,
            BlobName = blobName,
            ContentLength = fileInfo.Length,
            ContentType = descriptor?.ContentType ?? GetMimeType(blobName),
            LastModified = fileInfo.LastWriteTimeUtc,
            Metadata = descriptor?.Metadata ?? new Dictionary<string, string>()
        };
    }

    public Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blob '{blobName}' does not exist in container '{containerName}'.", filePath);
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream>(stream);
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<KyrolusBlobProperties?> GetPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<KyrolusBlobProperties?>(null);
        }

        var fileInfo = new FileInfo(filePath);
        var props = new KyrolusBlobProperties
        {
            ContainerName = containerName,
            BlobName = blobName,
            ContentLength = fileInfo.Length,
            ContentType = GetMimeType(blobName),
            LastModified = fileInfo.LastWriteTimeUtc
        };

        return Task.FromResult<KyrolusBlobProperties?>(props);
    }

    public Task<string> GetPresignedUrlAsync(string containerName, string blobName, TimeSpan expiry, bool isWrite = false, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(containerName, blobName);
        return Task.FromResult(new Uri(filePath).AbsoluteUri);
    }

    public Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var containerDir = Path.Combine(_rootPath, SanitizePath(containerName));
        if (!Directory.Exists(containerDir))
        {
            return Task.FromResult<IReadOnlyList<KyrolusBlobProperties>>([]);
        }

        var files = Directory.GetFiles(containerDir, "*", SearchOption.AllDirectories);
        var result = new List<KyrolusBlobProperties>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(containerDir, file).Replace('\\', '/');
            if (prefix is not null && !relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileInfo = new FileInfo(file);
            result.Add(new KyrolusBlobProperties
            {
                ContainerName = containerName,
                BlobName = relativePath,
                ContentLength = fileInfo.Length,
                ContentType = GetMimeType(relativePath),
                LastModified = fileInfo.LastWriteTimeUtc
            });
        }

        return Task.FromResult<IReadOnlyList<KyrolusBlobProperties>>(result);
    }

    private string GetFilePath(string containerName, string blobName)
    {
        return Path.Combine(_rootPath, SanitizePath(containerName), SanitizePath(blobName));
    }

    private static string SanitizePath(string name)
    {
        return name.TrimStart('/', '\\').Replace("..", "");
    }

    private static string GetMimeType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFileStorage(this IServiceCollection services, Action<KyrolusFileStorageOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IKyrolusStorageProvider, KyrolusFileStorageProvider>();
        return services;
    }
}
