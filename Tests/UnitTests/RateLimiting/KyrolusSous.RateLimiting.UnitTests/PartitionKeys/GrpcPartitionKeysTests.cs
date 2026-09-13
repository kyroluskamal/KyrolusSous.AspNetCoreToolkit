using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class GrpcPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "gRPC Service/Method, Call Type, and Timeout Deadline Budget resolve accurately")]
    public void Grpc_Keys_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // gRPC Service and Method
        KyrolusRateLimitPartitionKeys.GetGrpcServiceAndMethodKey(context).ShouldBe("grpc_non_rpc");
        context.Request.Path = "/billing.PaymentService/ProcessPayment";
        KyrolusRateLimitPartitionKeys.GetGrpcServiceAndMethodKey(context).ShouldBe("grpc_paymentservice_processpayment");
        KyrolusRateLimitPartitionKeys.GetGrpcServiceAndMethodAndClientKey(context).ShouldBe("grpc_paymentservice_processpayment:198.51.100.42");

        // gRPC Call Pattern
        var callContext = new DefaultHttpContext();
        callContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetGrpcCallTypeKey(callContext).ShouldBe("grpc_call_unknown");

        callContext.Request.ContentType = "application/grpc";
        KyrolusRateLimitPartitionKeys.GetGrpcCallTypeKey(callContext).ShouldBe("grpc_unary");

        callContext.Request.Headers["X-Grpc-Call-Type"] = "bidi_streaming";
        KyrolusRateLimitPartitionKeys.GetGrpcCallTypeKey(callContext).ShouldBe("grpc_bidi_streaming");
        KyrolusRateLimitPartitionKeys.GetGrpcCallTypeAndClientKey(callContext).ShouldBe("grpc_bidi_streaming:198.51.100.42");

        // gRPC Timeout Deadline Budget
        var deadlineContext = new DefaultHttpContext();
        deadlineContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetGrpcDeadlineBudgetBucketKey(deadlineContext).ShouldBe("grpc_deadline_none");

        // 500 milliseconds -> tight (<= 1s)
        deadlineContext.Request.Headers["grpc-timeout"] = "500m";
        KyrolusRateLimitPartitionKeys.GetGrpcDeadlineBudgetBucketKey(deadlineContext).ShouldBe("grpc_deadline_tight");
        KyrolusRateLimitPartitionKeys.GetGrpcDeadlineBudgetBucketAndClientKey(deadlineContext).ShouldBe("grpc_deadline_tight:198.51.100.42");

        // 10 seconds -> standard (<= 60s)
        deadlineContext.Request.Headers["grpc-timeout"] = "10S";
        KyrolusRateLimitPartitionKeys.GetGrpcDeadlineBudgetBucketKey(deadlineContext).ShouldBe("grpc_deadline_standard");

        // 2 minutes -> loose (> 60s)
        deadlineContext.Request.Headers["grpc-timeout"] = "2M";
        KyrolusRateLimitPartitionKeys.GetGrpcDeadlineBudgetBucketKey(deadlineContext).ShouldBe("grpc_deadline_loose");
    }
}
