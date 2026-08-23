using System.IO.Compression;

namespace KyrolusSous.Compression;

/// <summary>
/// Configuration options for the Kyrolus HTTP response compression middleware.
/// </summary>
public sealed class KyrolusResponseCompressionOptions
{
    /// <summary>
    /// Gets the set of MIME types that should be compressed.
    /// </summary>
    public HashSet<string> MimeTypes { get; set; } =
    [
        "application/json",
        "application/problem+json",
        "application/xml",
        "application/javascript",
        "application/x-javascript",
        "application/xhtml+xml",
        "application/ld+json",
        "text/plain",
        "text/html",
        "text/css",
        "text/xml",
        "text/csv",
        "text/javascript",
        "image/svg+xml"
    ];

    /// <summary>
    /// Gets the set of MIME types that should be excluded from compression.
    /// </summary>
    public HashSet<string> ExcludedMimeTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/avif",
        "video/mp4",
        "video/webm",
        "video/quicktime",
        "audio/mpeg",
        "audio/ogg",
        "application/pdf",
        "application/zip",
        "application/x-gzip",
        "application/x-brotli",
        "application/octet-stream",
        "text/event-stream"
    ];

    /// <summary>
    /// Gets the set of URL path prefixes that should be excluded from compression.
    /// </summary>
    public HashSet<string> ExcludedPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the minimum response size in bytes required to trigger compression.
    /// Responses smaller than this threshold will not be compressed. Defaults to 1024 bytes.
    /// </summary>
    public int MinSizeBytes { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the compression level. Defaults to <see cref="CompressionLevel.Fastest"/>.
    /// </summary>
    public CompressionLevel Level { get; set; } = CompressionLevel.Fastest;

    /// <summary>
    /// Gets or sets an optional preferred algorithm override.
    /// If null, the algorithm is negotiated automatically based on the request's Accept-Encoding header (Brotli > Zstd > Gzip > Deflate).
    /// </summary>
    public CompressionAlgorithm? PreferredAlgorithm { get; set; }

    /// <summary>
    /// Gets or sets whether response compression is enabled over HTTPS connections. Defaults to true.
    /// </summary>
    public bool EnableForHttps { get; set; } = true;

    /// <summary>
    /// Adds a MIME type to the compression whitelist.
    /// </summary>
    public KyrolusResponseCompressionOptions IncludeMimeType(string mimeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        MimeTypes.Add(mimeType.ToLowerInvariant());
        ExcludedMimeTypes.Remove(mimeType.ToLowerInvariant());
        return this;
    }

    /// <summary>
    /// Excludes a MIME type from compression.
    /// </summary>
    public KyrolusResponseCompressionOptions ExcludeMimeType(string mimeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ExcludedMimeTypes.Add(mimeType.ToLowerInvariant());
        MimeTypes.Remove(mimeType.ToLowerInvariant());
        return this;
    }

    /// <summary>
    /// Excludes a specific URL path prefix from compression (e.g. "/api/stream", "/signalr/hub").
    /// </summary>
    public KyrolusResponseCompressionOptions ExcludePath(string pathPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathPrefix);
        ExcludedPaths.Add(pathPrefix);
        return this;
    }

    /// <summary>
    /// Sets the minimum size threshold in bytes.
    /// </summary>
    public KyrolusResponseCompressionOptions WithMinSizeBytes(int bytes)
    {
        MinSizeBytes = Math.Max(0, bytes);
        return this;
    }

    /// <summary>
    /// Sets the preferred compression algorithm.
    /// </summary>
    public KyrolusResponseCompressionOptions WithPreferredAlgorithm(CompressionAlgorithm algorithm)
    {
        PreferredAlgorithm = algorithm;
        return this;
    }

    /// <summary>
    /// Sets the compression level.
    /// </summary>
    public KyrolusResponseCompressionOptions WithLevel(CompressionLevel level)
    {
        Level = level;
        return this;
    }
}
