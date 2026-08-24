namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisCacheHealthCheckTests
{
    [Fact(DisplayName = "KyrolusRedisCacheHealthCheck: When multiplexer is disconnected, should report Unhealthy")]
    public async Task HealthCheck_Disconnected_ReturnsUnhealthy()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        muxer.IsConnected.Returns(false);

        var healthCheck = new KyrolusRedisCacheHealthCheck(muxer);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, HealthStatus.Unhealthy, null)
        };

        var result = await healthCheck.CheckHealthAsync(context);
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("not connected");
    }

    [Fact(DisplayName = "KyrolusRedisCacheHealthCheck: When ping succeeds, should report Healthy")]
    public async Task HealthCheck_PingSuccess_ReturnsHealthy()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        db.PingAsync().Returns(Task.FromResult(TimeSpan.FromMilliseconds(5)));

        var healthCheck = new KyrolusRedisCacheHealthCheck(muxer);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, HealthStatus.Unhealthy, null)
        };

        var result = await healthCheck.CheckHealthAsync(context);
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ContainsKey("latency_ms").ShouldBeTrue();
    }
}
