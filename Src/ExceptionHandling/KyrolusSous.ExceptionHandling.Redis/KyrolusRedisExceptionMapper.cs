global using System.Net;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using KyrolusSous.ExceptionHandling.Abstractions.Helpers;
global using StackExchange.Redis;

namespace KyrolusSous.ExceptionHandling.Redis;

public sealed class KyrolusRedisExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -60;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is RedisConnectionException or RedisTimeoutException)
        {
            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.Timeout,
                title: "Redis timeout",
                statusCode: HttpStatusCode.GatewayTimeout,
                detail: exception.Message,
                traceId: context.TraceId,
                metadata: KyrolusMetadataExtractor.Extract(exception))
                .AsTransient();

            return true;
        }

        if (exception is RedisServerException or RedisException)
        {
            mapping = KyrolusExceptionMapping.Create(
                code: KyrolusErrorCodes.ExternalService,
                title: "Redis server error",
                statusCode: HttpStatusCode.BadGateway,
                detail: exception.Message,
                traceId: context.TraceId,
                metadata: KyrolusMetadataExtractor.Extract(exception))
                .AsTransient();

            return true;
        }

        mapping = null!;
        return false;
    }
}
