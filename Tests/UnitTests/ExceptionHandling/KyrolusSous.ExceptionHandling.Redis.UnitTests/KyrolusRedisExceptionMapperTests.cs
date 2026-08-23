using System.Net;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KyrolusSous.ExceptionHandling.Redis.UnitTests;

public class KyrolusRedisExceptionMapperTests
{
    private readonly KyrolusRedisExceptionMapper mapper = new();
    private readonly KyrolusErrorContext context = new(
        TraceId: "trace-redis-123",
        CorrelationId: "corr-redis-456",
        UserId: "user-redis",
        TenantId: "tenant-redis",
        Path: "/api/cache",
        Method: "GET",
        Culture: null);

    [Fact(DisplayName = "Order should return -60")]
    public void Order_Should_Be_Minus60()
    {
        mapper.Order.ShouldBe(-60);
    }

    [Fact(DisplayName = "TryMap with RedisTimeoutException should map to Timeout error")]
    public void TryMap_RedisTimeoutException_Should_Map_To_Timeout()
    {
        var ex = new RedisTimeoutException("Redis command timed out", CommandStatus.Unknown);
        ex.Data["Key"] = "session:user-1";

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        mapping.IsTransient.ShouldBeTrue();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.Timeout);
        mapping.Error.Title.ShouldBe("Redis timeout");
        mapping.Error.Detail.ShouldBe("Redis command timed out");
        mapping.Error.TraceId.ShouldBe("trace-redis-123");
        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata["Key"]!.ToString().ShouldBe("session:user-1");
    }

    [Fact(DisplayName = "TryMap with RedisConnectionException should map to Timeout error")]
    public void TryMap_RedisConnectionException_Should_Map_To_Timeout()
    {
        var ex = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Could not connect to redis endpoint");

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
        mapping.IsTransient.ShouldBeTrue();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.Timeout);
    }

    [Fact(DisplayName = "TryMap with RedisServerException should map to ExternalService error")]
    public void TryMap_RedisServerException_Should_Map_To_ExternalService()
    {
        var ex = new RedisServerException("ERR OOM command not allowed when used memory > 'maxmemory'");

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        mapping.IsTransient.ShouldBeTrue();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.ExternalService);
        mapping.Error.Title.ShouldBe("Redis server error");
        mapping.Error.Detail.ShouldBe("ERR OOM command not allowed when used memory > 'maxmemory'");
    }

    [Fact(DisplayName = "TryMap with unrelated exception should return false and null mapping")]
    public void TryMap_UnrelatedException_Should_Return_False()
    {
        var ex = new InvalidOperationException("Not a redis error");

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeFalse();
        mapping.ShouldBeNull();
    }

    [Fact(DisplayName = "AddKyrolusRedisExceptionHandling should register mapper in DI")]
    public void AddKyrolusRedisExceptionHandling_Should_Register_Mapper()
    {
        var services = new ServiceCollection();
        services.AddKyrolusRedisExceptionHandling();

        var provider = services.BuildServiceProvider();
        var mappers = provider.GetServices<IKyrolusExceptionMapper>().ToList();

        mappers.ShouldNotBeEmpty();
        mappers.ShouldContain(m => m is KyrolusRedisExceptionMapper);
    }
}
