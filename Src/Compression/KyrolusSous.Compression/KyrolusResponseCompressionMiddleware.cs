using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Compression;

/// <summary>
/// Middleware that automatically compresses HTTP response bodies based on MIME types, routes, and client Accept-Encoding.
/// </summary>
public sealed class KyrolusResponseCompressionMiddleware(
    RequestDelegate next,
    IOptions<KyrolusResponseCompressionOptions> options)
{
    private readonly KyrolusResponseCompressionOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
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

        var originalBody = context.Response.Body;
        await using var compressionWrapper = new ResponseCompressionStreamWrapper(
            context,
            originalBody,
            algorithm.Value,
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
        {
            return false;
        }

        var method = context.Request.Method;
        if (HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return false;
        }

        var path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var excludedPath in _options.ExcludedPaths)
            {
                if (path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private CompressionAlgorithm? DetermineAlgorithm(HttpContext context)
    {
        if (_options.PreferredAlgorithm.HasValue)
        {
            return _options.PreferredAlgorithm.Value;
        }

        var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
        if (string.IsNullOrWhiteSpace(acceptEncoding))
        {
            return null;
        }

        // Priority order: Brotli > Zstd > Gzip > Deflate
        if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
        {
            return CompressionAlgorithm.Brotli;
        }

        if (acceptEncoding.Contains("zstd", StringComparison.OrdinalIgnoreCase))
        {
            return CompressionAlgorithm.Zstd;
        }

        if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
        {
            return CompressionAlgorithm.Gzip;
        }

        if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
        {
            return CompressionAlgorithm.Deflate;
        }

        return null;
    }

    private sealed class ResponseCompressionStreamWrapper : Stream
    {
        private readonly HttpContext _context;
        private readonly Stream _originalStream;
        private readonly CompressionAlgorithm _algorithm;
        private readonly KyrolusResponseCompressionOptions _options;
        private Stream? _compressorStream;
        private bool _headersEvaluated;
        private bool _compressionEnabled;
        private bool _isDisposed;

        public ResponseCompressionStreamWrapper(
            HttpContext context,
            Stream originalStream,
            CompressionAlgorithm algorithm,
            KyrolusResponseCompressionOptions options)
        {
            _context = context;
            _originalStream = originalStream;
            _algorithm = algorithm;
            _options = options;
        }

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
            {
                _compressorStream.Flush();
            }
            _originalStream.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_compressionEnabled && _compressorStream is not null)
            {
                await _compressorStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            await _originalStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureHeadersEvaluated(count);

            if (_compressionEnabled && _compressorStream is not null)
            {
                _compressorStream.Write(buffer, offset, count);
            }
            else
            {
                _originalStream.Write(buffer, offset, count);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureHeadersEvaluated(count);

            if (_compressionEnabled && _compressorStream is not null)
            {
                await _compressorStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _originalStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureHeadersEvaluated(buffer.Length);

            if (_compressionEnabled && _compressorStream is not null)
            {
                await _compressorStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _originalStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FinishAsync()
        {
            if (_compressionEnabled && _compressorStream is not null)
            {
                await _compressorStream.FlushAsync().ConfigureAwait(false);
                if (_compressorStream is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _compressorStream.Dispose();
                }
                _compressorStream = null;
            }
        }

        private void EnsureHeadersEvaluated(int firstChunkSize)
        {
            if (_headersEvaluated)
            {
                return;
            }

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
            _context.Response.Headers.ContentEncoding = GetEncodingHeaderValue(_algorithm);
            _context.Response.Headers.Remove("Content-Length");

            _compressorStream = CreateCompressorStream(_originalStream, _algorithm, _options.Level);
        }

        private bool IsMimeTypeCompressible(string rawContentType)
        {
            var mimeType = rawContentType.Split(';')[0].Trim().ToLowerInvariant();

            if (_options.ExcludedMimeTypes.Contains(mimeType))
            {
                return false;
            }

            return _options.MimeTypes.Contains(mimeType);
        }

        private static string GetEncodingHeaderValue(CompressionAlgorithm algorithm) => algorithm switch
        {
            CompressionAlgorithm.Brotli => "br",
            CompressionAlgorithm.Zstd => "zstd",
            CompressionAlgorithm.Gzip => "gzip",
            CompressionAlgorithm.Deflate => "deflate",
            _ => "br"
        };

        private static Stream CreateCompressorStream(
            Stream destination,
            CompressionAlgorithm algorithm,
            CompressionLevel level) => algorithm switch
        {
            CompressionAlgorithm.Brotli => new BrotliStream(destination, level, leaveOpen: true),
            CompressionAlgorithm.Zstd => new ZstdSharp.CompressionStream(destination, MapZstdLevel(level), leaveOpen: true),
            CompressionAlgorithm.Gzip => new GZipStream(destination, level, leaveOpen: true),
            CompressionAlgorithm.Deflate => new DeflateStream(destination, level, leaveOpen: true),
            _ => new BrotliStream(destination, level, leaveOpen: true)
        };

        private static int MapZstdLevel(CompressionLevel level) => level switch
        {
            CompressionLevel.NoCompression => 1,
            CompressionLevel.Fastest => 1,
            CompressionLevel.Optimal => 3,
            CompressionLevel.SmallestSize => 19,
            _ => 3
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
                    _compressorStream?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
