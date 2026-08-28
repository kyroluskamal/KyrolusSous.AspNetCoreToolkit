using Amazon.S3;
using Amazon.S3.Model;
using KyrolusSous.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Storage.S3;

public sealed class KyrolusS3Options
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string? ServiceUrl { get; set; } // MinIO / custom endpoint
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true; // Required for MinIO
    public string? DefaultBucket { get; set; }
}

public sealed class KyrolusS3StorageProvider : IKyrolusStorageProvider, IKyrolusMultipartStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly KyrolusS3Options _options;

    public KyrolusS3StorageProvider(IAmazonS3 s3Client, IOptions<KyrolusS3Options> options)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options?.Value ?? new KyrolusS3Options();
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

        var putRequest = new PutObjectRequest
        {
            BucketName = containerName,
            Key = blobName,
            InputStream = contentStream,
            ContentType = descriptor?.ContentType ?? "application/octet-stream"
        };

        if (descriptor?.Metadata != null)
        {
            foreach (var kvp in descriptor.Metadata)
            {
                putRequest.Metadata.Add(kvp.Key, kvp.Value);
            }
        }

        var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

        return new KyrolusBlobProperties
        {
            ContainerName = containerName,
            BlobName = blobName,
            ContentLength = contentStream.CanSeek ? contentStream.Length : 0,
            ContentType = putRequest.ContentType,
            ETag = response.ETag,
            LastModified = DateTimeOffset.UtcNow,
            Metadata = descriptor?.Metadata ?? new Dictionary<string, string>()
        };
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var getRequest = new GetObjectRequest
        {
            BucketName = containerName,
            Key = blobName
        };

        var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken).ConfigureAwait(false);
        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(containerName, blobName, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var meta = await _s3Client.GetObjectMetadataAsync(containerName, blobName, cancellationToken).ConfigureAwait(false);
            return meta != null;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<KyrolusBlobProperties?> GetPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var meta = await _s3Client.GetObjectMetadataAsync(containerName, blobName, cancellationToken).ConfigureAwait(false);
            var metadataDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in meta.Metadata.Keys)
            {
                metadataDict[key] = meta.Metadata[key];
            }

            return new KyrolusBlobProperties
            {
                ContainerName = containerName,
                BlobName = blobName,
                ContentLength = meta.ContentLength,
                ContentType = meta.Headers.ContentType,
                ETag = meta.ETag,
                LastModified = meta.LastModified,
                Metadata = metadataDict
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<string> GetPresignedUrlAsync(string containerName, string blobName, TimeSpan expiry, bool isWrite = false, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = containerName,
            Key = blobName,
            Verb = isWrite ? HttpVerb.PUT : HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task<IReadOnlyList<KyrolusBlobProperties>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = containerName,
            Prefix = prefix
        };

        var response = await _s3Client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
        var result = new List<KyrolusBlobProperties>();

        foreach (var obj in response.S3Objects)
        {
            result.Add(new KyrolusBlobProperties
            {
                ContainerName = containerName,
                BlobName = obj.Key,
                ContentLength = obj.Size,
                ETag = obj.ETag,
                LastModified = obj.LastModified
            });
        }

        return result;
    }

    public async Task<KyrolusBlobProperties> CopyBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
    {
        var copyRequest = new CopyObjectRequest
        {
            SourceBucket = sourceContainer,
            SourceKey = sourceBlob,
            DestinationBucket = destinationContainer,
            DestinationKey = destinationBlob
        };

        var response = await _s3Client.CopyObjectAsync(copyRequest, cancellationToken).ConfigureAwait(false);
        return new KyrolusBlobProperties
        {
            ContainerName = destinationContainer,
            BlobName = destinationBlob,
            ETag = response.ETag,
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public async Task<KyrolusBlobProperties> MoveBlobAsync(string sourceContainer, string sourceBlob, string destinationContainer, string destinationBlob, CancellationToken cancellationToken = default)
    {
        var result = await CopyBlobAsync(sourceContainer, sourceBlob, destinationContainer, destinationBlob, cancellationToken).ConfigureAwait(false);
        await DeleteAsync(sourceContainer, sourceBlob, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<KyrolusMultipartUploadSession> InitiateMultipartUploadAsync(string containerName, string blobName, KyrolusBlobDescriptor? descriptor = null, CancellationToken cancellationToken = default)
    {
        var request = new InitiateMultipartUploadRequest
        {
            BucketName = containerName,
            Key = blobName,
            ContentType = descriptor?.ContentType ?? "application/octet-stream"
        };

        var response = await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
        return new KyrolusMultipartUploadSession
        {
            UploadId = response.UploadId,
            ContainerName = containerName,
            BlobName = blobName
        };
    }

    public async Task<KyrolusMultipartPartInfo> UploadPartAsync(KyrolusMultipartUploadSession session, int partNumber, Stream partStream, CancellationToken cancellationToken = default)
    {
        var request = new UploadPartRequest
        {
            BucketName = session.ContainerName,
            Key = session.BlobName,
            UploadId = session.UploadId,
            PartNumber = partNumber,
            InputStream = partStream
        };

        var response = await _s3Client.UploadPartAsync(request, cancellationToken).ConfigureAwait(false);
        return new KyrolusMultipartPartInfo
        {
            PartNumber = partNumber,
            ETag = response.ETag,
            Size = partStream.CanSeek ? partStream.Length : 0
        };
    }

    public async Task<KyrolusBlobProperties> CompleteMultipartUploadAsync(KyrolusMultipartUploadSession session, IEnumerable<KyrolusMultipartPartInfo> parts, CancellationToken cancellationToken = default)
    {
        var request = new CompleteMultipartUploadRequest
        {
            BucketName = session.ContainerName,
            Key = session.BlobName,
            UploadId = session.UploadId,
            PartETags = parts.OrderBy(p => p.PartNumber).Select(p => new PartETag(p.PartNumber, p.ETag)).ToList()
        };

        var response = await _s3Client.CompleteMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
        return new KyrolusBlobProperties
        {
            ContainerName = session.ContainerName,
            BlobName = session.BlobName,
            ETag = response.ETag,
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public async Task<bool> AbortMultipartUploadAsync(KyrolusMultipartUploadSession session, CancellationToken cancellationToken = default)
    {
        var request = new AbortMultipartUploadRequest
        {
            BucketName = session.ContainerName,
            Key = session.BlobName,
            UploadId = session.UploadId
        };

        await _s3Client.AbortMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusS3Storage(this IServiceCollection services, Action<KyrolusS3Options>? configure = null)
    {
        var options = new KyrolusS3Options();
        configure?.Invoke(options);

        services.Configure<KyrolusS3Options>(opt =>
        {
            opt.AccessKey = options.AccessKey;
            opt.SecretKey = options.SecretKey;
            opt.ServiceUrl = options.ServiceUrl;
            opt.Region = options.Region;
            opt.ForcePathStyle = options.ForcePathStyle;
            opt.DefaultBucket = options.DefaultBucket;
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var config = new AmazonS3Config
            {
                ForcePathStyle = options.ForcePathStyle
            };

            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
            }

            return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
        });

        services.AddSingleton<IKyrolusStorageProvider, KyrolusS3StorageProvider>();
        services.AddSingleton<IKyrolusMultipartStorageProvider>(sp => (KyrolusS3StorageProvider)sp.GetRequiredService<IKyrolusStorageProvider>());
        return services;
    }
}
