namespace KyrolusSous.Compression;

/// <summary>
/// Configuration options for the Kyrolus HTTP response compression middleware.
/// </summary>
public sealed class KyrolusResponseCompressionOptions
{
    /// <summary>
    /// Gets or sets the preferred compression algorithm when the client supports multiple encodings.
    /// Default is <see cref="KyrolusCompressionAlgorithm.Brotli"/>.
    /// </summary>
    public KyrolusCompressionAlgorithm PreferredAlgorithm { get; set; } = KyrolusCompressionAlgorithm.Brotli;

    /// <summary>
    /// Gets or sets the default compression level to apply during response compression.
    /// Default is <see cref="CompressionLevel.Fastest"/> for high throughput and low CPU overhead.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;

    /// <summary>
    /// Gets or sets the minimum response size in bytes required to trigger compression.
    /// Payloads smaller than this threshold will not be compressed (saving CPU on tiny responses).
    /// Default is 1024 bytes (1 KB).
    /// </summary>
    public long MinSizeBytes { get; set; } = 1024;

    /// <summary>
    /// Gets or sets whether response compression is enabled for HTTPS requests.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnableForHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets the set of MIME types that are eligible for compression.
    /// Standard web text formats (JSON, XML, HTML, JS, CSS, SVG) are included by default.
    /// </summary>
    public HashSet<string> IncludedMimeTypes { get; set; } =
    [
        "text/plain",
        "text/html",
        "text/css",
        "text/javascript",
        "text/xml",
        "text/csv",
        "text/markdown",
        "application/javascript",
        "application/json",
        "application/xml",
        "application/x-javascript",
        "application/graphql+json",
        "application/problem+json",
        "application/problem+xml",
        "application/ld+json",
        "application/manifest+json",
        "image/svg+xml"
    ];

    /// <summary>
    /// Gets or sets the set of MIME types explicitly excluded from compression.
    /// Already-compressed formats (JPEG, PNG, WebP, MP4, PDF, ZIP) are excluded by default to avoid negative compression and CPU waste.
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
    /// Gets or sets route path prefixes that should bypass response compression (e.g. "/api/streaming", "/hub/").
    /// </summary>
    public HashSet<string> ExcludedPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fluent helper to exclude a route prefix from compression.
    /// </summary>
    public KyrolusResponseCompressionOptions ExcludePath(string pathPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathPrefix);
        ExcludedPaths.Add(pathPrefix.StartsWith('/') ? pathPrefix : "/" + pathPrefix);
        return this;
    }

    /// <summary>
    /// Fluent helper to include a custom MIME type for compression.
    /// </summary>
    public KyrolusResponseCompressionOptions IncludeMimeType(string mimeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        IncludedMimeTypes.Add(mimeType);
        return this;
    }

    /// <summary>
    /// Fluent helper to exclude a MIME type from compression.
    /// </summary>
    public KyrolusResponseCompressionOptions ExcludeMimeType(string mimeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ExcludedMimeTypes.Add(mimeType);
        IncludedMimeTypes.Remove(mimeType);
        return this;
    }

    /// <summary>
    /// Fluent helper to set the minimum size threshold in bytes.
    /// </summary>
    public KyrolusResponseCompressionOptions WithMinSizeBytes(long minSizeBytes)
    {
        MinSizeBytes = Math.Max(0, minSizeBytes);
        return this;
    }

    /// <summary>
    /// Fluent helper to configure the preferred algorithm.
    /// </summary>
    public KyrolusResponseCompressionOptions WithPreferredAlgorithm(KyrolusCompressionAlgorithm algorithm)
    {
        PreferredAlgorithm = algorithm;
        return this;
    }
}
