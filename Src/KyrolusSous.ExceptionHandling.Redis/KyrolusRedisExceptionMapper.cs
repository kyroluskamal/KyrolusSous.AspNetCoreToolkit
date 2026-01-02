global using System.Net;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using StackExchange.Redis;

namespace KyrolusSous.ExceptionHandling.Redis;

public sealed class KyrolusRedisExceptionMapper : IKyrolusExceptionMapper
{
    public int Order => -60;

    public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
    {
        if (exception is RedisTimeoutException or RedisConnectionException)
        {
            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(KyrolusErrorCodes.Timeout, "Redis timeout", exception.Message, context.TraceId),
                HttpStatusCode.GatewayTimeout,
                IsTransient: true,
                ShouldLog: true);
            return true;
        }

        if (exception is RedisException)
        {
            mapping = new KyrolusExceptionMapping(
                new KyrolusErrorEnvelope(KyrolusErrorCodes.ExternalService, "Redis error", exception.Message, context.TraceId),
                HttpStatusCode.BadGateway,
                IsTransient: true,
                ShouldLog: true);
            return true;
        }

        mapping = null!;
        return false;
    }
}
