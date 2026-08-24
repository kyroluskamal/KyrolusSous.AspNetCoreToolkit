namespace KyrolusSous.Compression.UnitTests.Middleware;

public class KyrolusResponseCompressionMiddlewareTests
{
    private readonly KyrolusCompressionProvider _provider;

    public KyrolusResponseCompressionMiddlewareTests()
    {
        _provider = new KyrolusCompressionProvider();
        _provider.Register(BrotliCompressor.Instance);
        _provider.Register(ZstdCompressor.Instance);
        _provider.Register(Lz4Compressor.Instance);
        _provider.Register(SnappyCompressor.Instance);
        _provider.Register(GzipCompressor.Instance);
        _provider.Register(DeflateCompressor.Instance);
    }

    [Theory(DisplayName = "Middleware should compress response when client Accept-Encoding matches supported algorithm")]
    [InlineData("br", "br", CompressionAlgorithm.Brotli)]
    [InlineData("zstd", "zstd", CompressionAlgorithm.Zstd)]
    [InlineData("gzip", "gzip", CompressionAlgorithm.Gzip)]
    [InlineData("deflate", "deflate", CompressionAlgorithm.Deflate)]
    public async Task Middleware_ShouldCompressResponse_WhenEncodingIsSupported(
        string acceptEncoding,
        string expectedContentEncoding,
        CompressionAlgorithm algorithm)
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions
        {
            MinSizeBytes = 10,
            PreferredAlgorithm = algorithm
        });

        var payload = "{\"message\": \"Hello, Kyrolus Compression!\", \"data\": \"" + new string('A', 500) + "\"}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = acceptEncoding;
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes, 0, payloadBytes.Length);
                await ctx.Response.Body.FlushAsync();
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().ShouldBe(expectedContentEncoding);
        context.Response.Headers.ContainsKey("Content-Length").ShouldBeFalse();

        responseStream.Position = 0;
        var compressedBytes = responseStream.ToArray();
        compressedBytes.Length.ShouldBeGreaterThan(0);

        var decompressed = _provider.GetCompressor(algorithm).Decompress(compressedBytes);
        Encoding.UTF8.GetString(decompressed).ShouldBe(payload);
    }

    [Theory(DisplayName = "Middleware fallback negotiation should select client supported algorithm when preferred is unsupported")]
    [InlineData("br", "br", CompressionAlgorithm.Brotli)]
    [InlineData("zstd", "zstd", CompressionAlgorithm.Zstd)]
    [InlineData("gzip", "gzip", CompressionAlgorithm.Gzip)]
    [InlineData("deflate", "deflate", CompressionAlgorithm.Deflate)]
    public async Task Middleware_FallbackNegotiation_WhenPreferredAlgorithmNotSupported(
        string acceptEncoding,
        string expectedContentEncoding,
        CompressionAlgorithm expectedAlgorithm)
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions
        {
            MinSizeBytes = 10,
            PreferredAlgorithm = CompressionAlgorithm.Lz4
        });

        var payload = "{\"fallback\": true, \"data\": \"" + new string('F', 300) + "\"}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = acceptEncoding;
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().ShouldBe(expectedContentEncoding);
        responseStream.Position = 0;
        var decompressed = _provider.GetCompressor(expectedAlgorithm).Decompress(responseStream.ToArray());
        Encoding.UTF8.GetString(decompressed).ShouldBe(payload);
    }

    [Fact(DisplayName = "Middleware when Accept-Encoding is empty or unsupported should bypass compression")]
    public async Task Middleware_WhenAcceptEncodingIsEmptyOrUnsupported_ShouldNotCompress()
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions { MinSizeBytes = 10 });
        var payload = "{\"test\": \"plain\"}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "unsupported-encoding";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Fact(DisplayName = "Middleware when requested algorithm is not registered in provider should bypass compression")]
    public async Task Middleware_WhenRequestedAlgorithmNotRegistered_ShouldBypassCompression()
    {
        var emptyProvider = new KyrolusCompressionProvider();

        var options = Options.Create(new KyrolusResponseCompressionOptions { MinSizeBytes = 10 });
        var payloadBytes = Encoding.UTF8.GetBytes("{\"unregistered\": true}");

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            emptyProvider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Theory(DisplayName = "Middleware when MIME type is excluded should write uncompressed response")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("application/zip")]
    [InlineData("application/octet-stream")]
    public async Task Middleware_WhenMimeTypeIsExcluded_ShouldNotCompress(string excludedMimeType)
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions { MinSizeBytes = 10 });
        var payloadBytes = new byte[2048];
        new Random(1).NextBytes(payloadBytes);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br, gzip";
        context.Response.ContentType = excludedMimeType;

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Body.Write(payloadBytes, 0, 1024);
                await ctx.Response.Body.WriteAsync(payloadBytes, 1024, 1024);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Fact(DisplayName = "Middleware when ContentType is empty or null should write uncompressed response")]
    public async Task Middleware_WhenContentTypeIsEmptyOrNull_ShouldNotCompress()
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions { MinSizeBytes = 10 });
        var payloadBytes = Encoding.UTF8.GetBytes("unknown content");

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = null;

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes, 0, payloadBytes.Length);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Theory(DisplayName = "Middleware when StatusCode is 204 or 304 should bypass compression")]
    [InlineData(204)]
    [InlineData(304)]
    public async Task Middleware_WhenStatusCode204Or304AndWritingBody_ShouldNotCompress(int statusCode)
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions { MinSizeBytes = 10 });
        var payloadBytes = Encoding.UTF8.GetBytes("no content payload");

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = statusCode;
                ctx.Response.Body.Write(payloadBytes, 0, 5);
                await ctx.Response.Body.WriteAsync(payloadBytes, 5, payloadBytes.Length - 5);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Fact(DisplayName = "Middleware when route path is excluded should bypass compression")]
    public async Task Middleware_WhenRouteIsExcluded_ShouldNotCompress()
    {
        var options = new KyrolusResponseCompressionOptions { MinSizeBytes = 10 };
        options.ExcludePath("/api/stream");

        var payloadBytes = Encoding.UTF8.GetBytes("{\"stream\": true, \"data\": \"" + new string('S', 500) + "\"}");

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/stream/live-feed";
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            Options.Create(options),
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Fact(DisplayName = "Middleware when HTTPS is disabled and request is HTTPS should bypass compression")]
    public async Task Middleware_WhenHttpsDisabledAndRequestIsHttps_ShouldNotCompress()
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions
        {
            EnableForHttps = false,
            MinSizeBytes = 10
        });

        var payloadBytes = Encoding.UTF8.GetBytes("{\"https\": true}");

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Theory(DisplayName = "Middleware when HTTP method is HEAD, OPTIONS, or TRACE should bypass compression")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public async Task Middleware_WhenMethodIsBypassed_ShouldNotCompress(string method)
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions { MinSizeBytes = 10 });
        var payloadBytes = Encoding.UTF8.GetBytes("{\"status\": \"ok\"}");

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
    }

    [Fact(DisplayName = "Middleware when response size is smaller than MinSizeBytes threshold should not compress")]
    public async Task Middleware_WhenPayloadIsSmallerThanMinSizeBytes_ShouldNotCompress()
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions
        {
            MinSizeBytes = 1024
        });

        var tinyPayload = "{\"small\": true}";
        var payloadBytes = Encoding.UTF8.GetBytes(tinyPayload);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";
        context.Response.ContentLength = payloadBytes.Length;

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Encoding").ShouldBeFalse();
        responseStream.ToArray().ShouldBe(payloadBytes);
    }

    [Fact(DisplayName = "ResponseCompressionStreamWrapper synchronous write, flush, and stream properties should behave correctly")]
    public async Task Middleware_SyncWriteAndFlush_And_StreamProperties_ShouldBehaveCorrectly()
    {
        var options = Options.Create(new KyrolusResponseCompressionOptions
        {
            MinSizeBytes = 10
        });

        var payload = "Sync stream writing test with Kyrolus Response Compression!";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            ctx =>
            {
                var body = ctx.Response.Body;
                body.CanRead.ShouldBeFalse();
                body.CanSeek.ShouldBeFalse();
                body.CanWrite.ShouldBeTrue();

                Should.Throw<NotSupportedException>(() => _ = body.Length);
                Should.Throw<NotSupportedException>(() => _ = body.Position);
                Should.Throw<NotSupportedException>(() => body.Position = 0);
                Should.Throw<NotSupportedException>(() => body.Read(new byte[10], 0, 10));
                Should.Throw<NotSupportedException>(() => body.Seek(0, SeekOrigin.Begin));
                Should.Throw<NotSupportedException>(() => body.SetLength(100));

                body.Write(payloadBytes, 0, payloadBytes.Length);
                body.Flush();
                body.FlushAsync().GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            options,
            _provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().ShouldBe("br");
    }

    [Fact(DisplayName = "ResponseCompressionStreamWrapper with non-IAsyncDisposable stream should dispose synchronously")]
    public async Task Middleware_WithNonAsyncDisposableCompressorStream_ShouldDisposeSynchronously()
    {
        var customCompressor = new SynchronousOnlyCompressor();
        var provider = new KyrolusCompressionProvider();
        provider.Register(customCompressor);

        var options = Options.Create(new KyrolusResponseCompressionOptions
        {
            MinSizeBytes = 10,
            PreferredAlgorithm = CompressionAlgorithm.Brotli
        });

        var payloadBytes = Encoding.UTF8.GetBytes("Testing non async disposable stream disposal in FinishAsync.");

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptEncoding = "br";
        context.Response.ContentType = "application/json";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new KyrolusResponseCompressionMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Body.WriteAsync(payloadBytes);
            },
            options,
            provider);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentEncoding.ToString().ShouldBe("br");
    }

    private sealed class SynchronousOnlyCompressor : ICompressor
    {
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.Brotli;
        public byte[] Compress(ReadOnlySpan<byte> data) => data.ToArray();
        public byte[] Decompress(ReadOnlySpan<byte> compressedData) => compressedData.ToArray();
        public Task CompressAsync(Stream s, Stream d, CompressionLevel l = CompressionLevel.Fastest, CancellationToken ct = default) => s.CopyToAsync(d, ct);
        public Task DecompressAsync(Stream s, Stream d, CancellationToken ct = default) => s.CopyToAsync(d, ct);
        public Stream CreateCompressionStream(Stream outputStream, CompressionLevel level = CompressionLevel.Fastest, bool leaveOpen = false) => new SynchronousOnlyStream(outputStream);
        public Stream CreateDecompressionStream(Stream inputStream, bool leaveOpen = false) => inputStream;
    }

    private sealed class SynchronousOnlyStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing) { base.Dispose(disposing); }
    }
}
