namespace KyrolusSous.Compression;

/// <summary>
/// Middleware that automatically compresses HTTP response bodies based on MIME types, routes, and client Accept-Encoding.
/// Decoupled from specific compressor implementations via <see cref="IKyrolusCompressionProvider"/>.
/// </summary>
public sealed class KyrolusResponseCompressionMiddleware(
    RequestDelegate next,
    IOptions<KyrolusResponseCompressionOptions> options,
    IKyrolusCompressionProvider? provider = null)
{
    private readonly KyrolusResponseCompressionOptions _options = options?.Value ?? new KyrolusResponseCompressionOptions();
    private readonly IKyrolusCompressionProvider _provider = provider ?? KyrolusCompressionProvider.Instance;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldProcessRequest(context))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var algorithm = DetermineAlgorithm(context);
        if (algorithm is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!_provider.TryGetCompressor(algorithm.Value, out var compressor) || compressor is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var originalBody = context.Response.Body;
        await using var compressionWrapper = new ResponseCompressionStreamWrapper(
            context,
            originalBody,
            compressor,
            _options);

        context.Response.Body = compressionWrapper;

        try
        {
            await next(context).ConfigureAwait(false);
            await compressionWrapper.FinishAsync().ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private bool ShouldProcessRequest(HttpContext context)
    {
        if (context.Request.IsHttps && !_options.EnableForHttps)
            return false;

        var method = context.Request.Method;

        if (HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            return false;

        var path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path))
            foreach (var excludedPath in _options.ExcludedPaths)
                if (path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
                    return false;

        return true;
    }

    private KyrolusCompressionAlgorithm? DetermineAlgorithm(HttpContext context)
    {
        var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
        if (string.IsNullOrWhiteSpace(acceptEncoding)) return null;

        var trimmed = acceptEncoding.Trim();
        if (string.Equals(trimmed, "identity", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("*;q=0", StringComparison.OrdinalIgnoreCase))
            return null;

        // Priority order: PreferredAlgorithm (if matched in Accept-Encoding) -> Brotli > Zstd > Gzip > Deflate
        if (IsEncodingSupported(acceptEncoding, _options.PreferredAlgorithm))
            return _options.PreferredAlgorithm;

        if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
            return KyrolusCompressionAlgorithm.Brotli;

        if (acceptEncoding.Contains("zstd", StringComparison.OrdinalIgnoreCase))
            return KyrolusCompressionAlgorithm.Zstd;

        if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
            return KyrolusCompressionAlgorithm.Gzip;

        if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
            return KyrolusCompressionAlgorithm.Deflate;

        return null;
    }

    private static bool IsEncodingSupported(string acceptEncoding, KyrolusCompressionAlgorithm algorithm) => algorithm switch
    {
        KyrolusCompressionAlgorithm.Brotli => acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase),
        KyrolusCompressionAlgorithm.Zstd => acceptEncoding.Contains("zstd", StringComparison.OrdinalIgnoreCase),
        KyrolusCompressionAlgorithm.Gzip => acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase),
        KyrolusCompressionAlgorithm.Deflate => acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private sealed class ResponseCompressionStreamWrapper(
        HttpContext context,
        Stream originalStream,
        IKyrolusCompressor compressor,
        KyrolusResponseCompressionOptions options) : Stream
    {
        private readonly HttpContext _context = context;
        private readonly Stream _originalStream = originalStream;
        private readonly IKyrolusCompressor _compressor = compressor;
        private readonly KyrolusResponseCompressionOptions _options = options;
        private Stream? _compressorStream;
        private bool _headersEvaluated;
        private bool _compressionEnabled;
        private bool _isDisposed;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            if (_compressionEnabled && _compressorStream is not null)
                _compressorStream.Flush();
            _originalStream.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_compressionEnabled && _compressorStream is not null)
                await _compressorStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _originalStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureHeadersEvaluated(count);

            if (_compressionEnabled && _compressorStream is not null)
                _compressorStream.Write(buffer, offset, count);
            else
                _originalStream.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureHeadersEvaluated(count);

            if (_compressionEnabled && _compressorStream is not null)
                await _compressorStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            else
                await _originalStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureHeadersEvaluated(buffer.Length);

            if (_compressionEnabled && _compressorStream is not null)
                await _compressorStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            else
                await _originalStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public async Task FinishAsync()
        {
            if (!_isDisposed && _compressionEnabled && _compressorStream is not null)
            {
                await _compressorStream.FlushAsync().ConfigureAwait(false);
                await _compressorStream.DisposeAsync().ConfigureAwait(false);
                _compressorStream = null;
            }
        }

        private void EnsureHeadersEvaluated(int firstChunkSize)
        {
            if (_headersEvaluated) return;

            _headersEvaluated = true;

            if (_context.Response.StatusCode == StatusCodes.Status204NoContent ||
                _context.Response.StatusCode == StatusCodes.Status304NotModified)
            {
                _compressionEnabled = false;
                return;
            }

            if (firstChunkSize < _options.MinSizeBytes && _context.Response.ContentLength.HasValue &&
                _context.Response.ContentLength.Value < _options.MinSizeBytes)
            {
                _compressionEnabled = false;
                return;
            }

            var contentType = _context.Response.ContentType;
            if (string.IsNullOrWhiteSpace(contentType) || !IsMimeTypeCompressible(contentType))
            {
                _compressionEnabled = false;
                return;
            }

            // Enable compression
            _compressionEnabled = true;
            _context.Response.Headers.ContentEncoding = GetEncodingHeaderValue(_compressor.Algorithm);
            _context.Response.Headers.Remove("Content-Length");
            _context.Response.Headers.Append("Vary", "Accept-Encoding");

            _compressorStream = _compressor.CreateCompressionStream(_originalStream, _options.CompressionLevel, leaveOpen: true);
        }

        private bool IsMimeTypeCompressible(string rawContentType)
        {
            var mimeType = rawContentType.Split(';')[0].Trim().ToLowerInvariant();

            if (_options.ExcludedMimeTypes.Contains(mimeType)) return false;

            return _options.IncludedMimeTypes.Contains(mimeType);
        }

        private static string GetEncodingHeaderValue(KyrolusCompressionAlgorithm algorithm) => algorithm switch
        {
            KyrolusCompressionAlgorithm.Zstd => "zstd",
            KyrolusCompressionAlgorithm.Gzip => "gzip",
            KyrolusCompressionAlgorithm.Deflate => "deflate",
            _ => "br"
        };

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (disposing)
                {
                    _compressorStream?.Flush();
                    _compressorStream?.Dispose();
                    _compressorStream = null;
                }
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (_compressorStream is not null)
                {
                    await _compressorStream.FlushAsync().ConfigureAwait(false);
                    await _compressorStream.DisposeAsync().ConfigureAwait(false);
                    _compressorStream = null;
                }
            }
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
