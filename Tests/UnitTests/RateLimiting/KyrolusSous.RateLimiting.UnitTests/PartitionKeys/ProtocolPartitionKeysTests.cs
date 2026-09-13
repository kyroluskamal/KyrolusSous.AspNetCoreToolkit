using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class ProtocolPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Protocol and HTTP Wire Protocol version resolve accurately")]
    public void Protocol_And_HttpWireProtocol_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.Request.Protocol = "HTTP/2";

        KyrolusRateLimitPartitionKeys.GetProtocolAndClientKey(context).ShouldBe("proto_HTTP/2:198.51.100.42");

        // HTTP Wire Protocol
        KyrolusRateLimitPartitionKeys.GetHttpProtocolKey(context).ShouldBe("proto_http2");
        KyrolusRateLimitPartitionKeys.GetHttpProtocolAndClientKey(context).ShouldBe("proto_http2:198.51.100.42");

        context.Request.Protocol = "HTTP/3";
        KyrolusRateLimitPartitionKeys.GetHttpProtocolKey(context).ShouldBe("proto_http3");

        context.Request.Protocol = "HTTP/1.1";
        KyrolusRateLimitPartitionKeys.GetHttpProtocolKey(context).ShouldBe("proto_http11");
    }

    [Fact(DisplayName = "Transfer-Encoding, Connection Persistence, and Concurrent Streams resolve accurately")]
    public void TransferEncoding_ConnectionHeader_And_StreamDepth_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Transfer-Encoding
        KyrolusRateLimitPartitionKeys.GetTransferEncodingKey(context).ShouldBe("transfer_identity");
        context.Request.Headers.TransferEncoding = "chunked";
        KyrolusRateLimitPartitionKeys.GetTransferEncodingKey(context).ShouldBe("transfer_chunked");
        KyrolusRateLimitPartitionKeys.GetTransferEncodingAndClientKey(context).ShouldBe("transfer_chunked:198.51.100.42");

        // Connection Persistence
        var connContext = new DefaultHttpContext();
        connContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetConnectionHeaderKey(connContext).ShouldBe("conn_mode_keepalive");

        connContext.Request.Headers.Connection = "close";
        KyrolusRateLimitPartitionKeys.GetConnectionHeaderKey(connContext).ShouldBe("conn_mode_close");
        KyrolusRateLimitPartitionKeys.GetConnectionHeaderAndClientKey(connContext).ShouldBe("conn_mode_close:198.51.100.42");

        connContext.Request.Headers.Connection = "Upgrade";
        KyrolusRateLimitPartitionKeys.GetConnectionHeaderKey(connContext).ShouldBe("conn_mode_upgrade");

        // Concurrent Stream Depth
        var streamDepthContext = new DefaultHttpContext();
        streamDepthContext.Connection.RemoteIpAddress = BaseIp;
        streamDepthContext.Request.Headers["X-Concurrent-Streams"] = "25";
        KyrolusRateLimitPartitionKeys.GetConcurrentStreamBucketKey(streamDepthContext).ShouldBe("stream_depth_moderate");
        KyrolusRateLimitPartitionKeys.GetConcurrentStreamBucketAndClientKey(streamDepthContext).ShouldBe("stream_depth_moderate:198.51.100.42");

        streamDepthContext.Request.Headers["X-Concurrent-Streams"] = "80";
        KyrolusRateLimitPartitionKeys.GetConcurrentStreamBucketKey(streamDepthContext).ShouldBe("stream_depth_saturated");

        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRateLimitPartitionKeys.GetConcurrentStreamBucketKey(streamDepthContext, moderateThreshold: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRateLimitPartitionKeys.GetConcurrentStreamBucketKey(streamDepthContext, moderateThreshold: 20, saturatedThreshold: 10));
    }

    [Fact(DisplayName = "HTTP Priority, WebSocket Subprotocol, Streaming Protocol, and Range Request resolve accurately")]
    public void Priority_WebSocket_Streaming_And_Range_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // RFC 9218 HTTP Priority
        context.Request.Headers["Priority"] = "u=1, i";
        KyrolusRateLimitPartitionKeys.GetHttpPriorityKey(context).ShouldBe("priority_u1");
        KyrolusRateLimitPartitionKeys.GetHttpPriorityAndClientKey(context).ShouldBe("priority_u1:198.51.100.42");

        // WebSocket Subprotocol
        var wsContext = new DefaultHttpContext();
        wsContext.Connection.RemoteIpAddress = BaseIp;
        wsContext.Request.Headers["Sec-WebSocket-Protocol"] = "graphql-transport-ws, json";
        KyrolusRateLimitPartitionKeys.GetWebSocketSubprotocolKey(wsContext).ShouldBe("ws_subproto_graphql-transport-ws");
        KyrolusRateLimitPartitionKeys.GetWebSocketSubprotocolAndClientKey(wsContext).ShouldBe("ws_subproto_graphql-transport-ws:198.51.100.42");

        // Streaming Protocol
        var streamContext = new DefaultHttpContext();
        streamContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetStreamingProtocolKey(streamContext).ShouldBe("stream_unary");

        streamContext.Request.Headers["Upgrade"] = "websocket";
        KyrolusRateLimitPartitionKeys.GetStreamingProtocolKey(streamContext).ShouldBe("stream_websocket");
        KyrolusRateLimitPartitionKeys.GetStreamingProtocolAndClientKey(streamContext).ShouldBe("stream_websocket:198.51.100.42");

        var sseContext = new DefaultHttpContext();
        sseContext.Request.Headers.Accept = "text/event-stream";
        KyrolusRateLimitPartitionKeys.GetStreamingProtocolKey(sseContext).ShouldBe("stream_sse");

        var grpcContext = new DefaultHttpContext();
        grpcContext.Request.ContentType = "application/grpc";
        KyrolusRateLimitPartitionKeys.GetStreamingProtocolKey(grpcContext).ShouldBe("stream_grpc");

        // Range Request
        var rangeContext = new DefaultHttpContext();
        rangeContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetRangeRequestKey(rangeContext).ShouldBe("range_full");

        rangeContext.Request.Headers.Range = "bytes=0-1024";
        KyrolusRateLimitPartitionKeys.GetRangeRequestKey(rangeContext).ShouldBe("range_chunked");
        KyrolusRateLimitPartitionKeys.GetRangeRequestAndClientKey(rangeContext).ShouldBe("range_chunked:198.51.100.42");
    }
}
