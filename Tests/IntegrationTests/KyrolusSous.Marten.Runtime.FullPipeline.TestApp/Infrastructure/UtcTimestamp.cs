namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

internal static class UtcTimestamp
{
    private const long TimestampPrecisionTicks = 10L;

    internal static DateTimeOffset DateTimeOffsetNow() => Normalize(DateTimeOffset.UtcNow);

    internal static DateTime DateTimeNow() => Normalize(DateTime.UtcNow);

    internal static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var ticks = utc.Ticks - (utc.Ticks % TimestampPrecisionTicks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    internal static DateTime Normalize(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var ticks = utc.Ticks - (utc.Ticks % TimestampPrecisionTicks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
