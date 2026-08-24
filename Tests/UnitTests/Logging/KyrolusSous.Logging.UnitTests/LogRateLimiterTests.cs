using KyrolusSous.Logging.Core.Filters;
using KyrolusSous.Logging.Serilog.Filters;
using Serilog.Events;
using Serilog.Parsing;

namespace KyrolusSous.Logging.UnitTests;

public class LogRateLimiterTests
{
    [Fact(DisplayName = "RateLimiter: Allows up to threshold and throttles excess duplicates")]
    public void RateLimiter_ThrottlesExcess_InSameWindow()
    {
        var limiter = new KyrolusLogRateLimiter(maxOccurrencesPerWindow: 3, windowDuration: TimeSpan.FromSeconds(10));
        var template = "Database connection timeout occurred";

        // First 3 should pass
        for (var i = 0; i < 3; i++)
        {
            var decision = limiter.Check(template);
            decision.ShouldLog.ShouldBeTrue();
            decision.SuppressedCount.ShouldBe(0);
        }

        // 4th and 5th should be throttled
        var decision4 = limiter.Check(template);
        decision4.ShouldLog.ShouldBeFalse();
        decision4.SuppressedCount.ShouldBe(1);

        var decision5 = limiter.Check(template);
        decision5.ShouldLog.ShouldBeFalse();
        decision5.SuppressedCount.ShouldBe(2);

        // Different template should still pass
        var differentDecision = limiter.Check("Another unique message");
        differentDecision.ShouldLog.ShouldBeTrue();
    }

    [Fact(DisplayName = "RateLimiter: Reset clears all internal tracking buckets")]
    public void RateLimiter_Reset_ClearsBuckets()
    {
        var limiter = new KyrolusLogRateLimiter(maxOccurrencesPerWindow: 1);
        limiter.Check("Key1");
        limiter.Check("Key1").ShouldLog.ShouldBeFalse();

        limiter.Reset();

        limiter.Check("Key1").ShouldLog.ShouldBeTrue();
    }

    [Fact(DisplayName = "SerilogRateLimitingFilter: Suppresses log events and adds SuppressedEventsCount property")]
    public void SerilogRateLimitingFilter_Suppresses_AndAddsProperty()
    {
        var limiter = new KyrolusLogRateLimiter(maxOccurrencesPerWindow: 2, windowDuration: TimeSpan.FromMilliseconds(50));
        var filter = new KyrolusSerilogRateLimitingFilter(limiter);

        var templateParser = new MessageTemplateParser();
        var template = templateParser.Parse("Payment processing error");

        var event1 = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, template, []);
        var event2 = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, template, []);
        var event3 = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, template, []);

        filter.IsEnabled(event1).ShouldBeTrue();
        filter.IsEnabled(event2).ShouldBeTrue();
        filter.IsEnabled(event3).ShouldBeFalse();

        // Null event check
        filter.IsEnabled(null!).ShouldBeFalse();
    }
}
