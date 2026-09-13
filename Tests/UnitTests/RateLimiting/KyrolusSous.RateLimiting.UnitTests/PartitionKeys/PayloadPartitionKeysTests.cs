using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class PayloadPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Payload Size Bucket and Multipart File Count resolve accurately")]
    public void PayloadSize_And_MultipartCount_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Payload Size Bucket
        context.Request.ContentLength = 500;
        KyrolusRateLimitPartitionKeys.GetPayloadSizeBucketKey(context).ShouldBe("payload_small");
        KyrolusRateLimitPartitionKeys.GetPayloadSizeBucketAndClientKey(context).ShouldBe("payload_small:198.51.100.42");

        context.Request.ContentLength = 15000;
        KyrolusRateLimitPartitionKeys.GetPayloadSizeBucketKey(context).ShouldBe("payload_small");

        context.Request.ContentLength = 200000;
        KyrolusRateLimitPartitionKeys.GetPayloadSizeBucketKey(context).ShouldBe("payload_medium");

        context.Request.ContentLength = 5000000;
        KyrolusRateLimitPartitionKeys.GetPayloadSizeBucketKey(context).ShouldBe("payload_large");

        // Multipart File Count
        var noFormContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetMultipartFileCountKey(noFormContext).ShouldBe("files_none");

        var formCtx = new DefaultHttpContext();
        formCtx.Connection.RemoteIpAddress = BaseIp;
        formCtx.Request.ContentType = "multipart/form-data";
        var formFiles = new FormFileCollection
        {
            new FormFile(Stream.Null, 0, 0, "f1", "doc1.pdf"),
            new FormFile(Stream.Null, 0, 0, "f2", "doc2.pdf")
        };
        formCtx.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), formFiles);
        KyrolusRateLimitPartitionKeys.GetMultipartFileCountKey(formCtx).ShouldBe("files_batch");
        KyrolusRateLimitPartitionKeys.GetMultipartFileCountAndClientKey(formCtx).ShouldBe("files_batch:198.51.100.42");
    }

    [Fact(DisplayName = "Compression Encoding and Accept-Encoding resolve accurately")]
    public void CompressionEncoding_And_AcceptEncoding_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Content-Encoding
        context.Request.Headers.ContentEncoding = "gzip";
        KyrolusRateLimitPartitionKeys.GetCompressionEncodingKey(context).ShouldBe("encoding_gzip");
        KyrolusRateLimitPartitionKeys.GetCompressionEncodingAndClientKey(context).ShouldBe("encoding_gzip:198.51.100.42");

        context.Request.Headers.ContentEncoding = "br";
        KyrolusRateLimitPartitionKeys.GetCompressionEncodingKey(context).ShouldBe("encoding_brotli");

        context.Request.Headers.ContentEncoding = "zstd";
        KyrolusRateLimitPartitionKeys.GetCompressionEncodingKey(context).ShouldBe("encoding_zstd");

        context.Request.Headers.ContentEncoding = "deflate";
        KyrolusRateLimitPartitionKeys.GetCompressionEncodingKey(context).ShouldBe("encoding_deflate");

        // Accept-Encoding
        var acceptEncContext = new DefaultHttpContext();
        acceptEncContext.Connection.RemoteIpAddress = BaseIp;
        acceptEncContext.Request.Headers.AcceptEncoding = "gzip, deflate, br, zstd";
        KyrolusRateLimitPartitionKeys.GetAcceptEncodingKey(acceptEncContext).ShouldBe("encoding_zstd");
        KyrolusRateLimitPartitionKeys.GetAcceptEncodingAndClientKey(acceptEncContext).ShouldBe("encoding_zstd:198.51.100.42");
    }

    [Fact(DisplayName = "ContentType Family, Header Overhead, Accept Media Type, and Content Digest resolve accurately")]
    public void ContentType_Overhead_MediaType_And_Digest_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // ContentType Family
        context.Request.ContentType = "application/json; charset=utf-8";
        KyrolusRateLimitPartitionKeys.GetContentTypeFamilyKey(context).ShouldBe("format_json");
        KyrolusRateLimitPartitionKeys.GetContentTypeFamilyAndClientKey(context).ShouldBe("format_json:198.51.100.42");

        context.Request.ContentType = "application/grpc";
        KyrolusRateLimitPartitionKeys.GetContentTypeFamilyKey(context).ShouldBe("format_other");

        // Header Overhead
        var overheadContext = new DefaultHttpContext();
        overheadContext.Connection.RemoteIpAddress = BaseIp;
        overheadContext.Request.Headers["X-Custom"] = "short";
        KyrolusRateLimitPartitionKeys.GetHeaderOverheadBucketKey(overheadContext).ShouldBe("hdroverhead_normal");
        KyrolusRateLimitPartitionKeys.GetHeaderOverheadBucketAndClientKey(overheadContext).ShouldBe("hdroverhead_normal:198.51.100.42");

        // Accept Media Type
        var mediaContext = new DefaultHttpContext();
        mediaContext.Connection.RemoteIpAddress = BaseIp;
        mediaContext.Request.Headers.Accept = "application/pdf";
        KyrolusRateLimitPartitionKeys.GetAcceptMediaTypeKey(mediaContext).ShouldBe("media_heavy");
        KyrolusRateLimitPartitionKeys.GetAcceptMediaTypeAndClientKey(mediaContext).ShouldBe("media_heavy:198.51.100.42");

        // RFC 9530 Content Digest
        var digestContext = new DefaultHttpContext();
        digestContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetContentDigestStateKey(digestContext).ShouldBe("digest_missing");

        digestContext.Request.Headers["Content-Digest"] = "sha-256=:4REjxQ3YrqGhT3PceO+PwY61yOM=:";
        KyrolusRateLimitPartitionKeys.GetContentDigestStateKey(digestContext).ShouldBe("digest_present");
        KyrolusRateLimitPartitionKeys.GetContentDigestStateAndClientKey(digestContext).ShouldBe("digest_present:198.51.100.42");
    }
}
