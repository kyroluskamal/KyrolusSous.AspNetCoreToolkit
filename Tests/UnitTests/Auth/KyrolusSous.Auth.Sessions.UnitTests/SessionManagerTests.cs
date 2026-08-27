using KyrolusSous.Auth.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Sessions.UnitTests;

public class SessionManagerTests
{
    private readonly KyrolusInMemorySessionStore _store = new();
    private readonly KyrolusSessionManager _manager;

    public SessionManagerTests()
    {
        _manager = new KyrolusSessionManager(_store);
    }

    [Fact]
    public async Task StartSession_CreatesActiveSession()
    {
        var session = await _manager.StartSessionAsync(
            userId: "user-100",
            ipAddress: "192.168.1.1",
            userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            deviceInfo: "Chrome on Windows");

        session.ShouldNotBeNull();
        session.SessionId.ShouldNotBeNullOrWhiteSpace();
        session.UserId.ShouldBe("user-100");
        session.IpAddress.ShouldBe("192.168.1.1");
        session.IsActive().ShouldBeTrue();

        var isValid = await _manager.ValidateSessionAsync(session.SessionId);
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeSession_InvalidatesSession()
    {
        var session = await _manager.StartSessionAsync("user-200");

        await _manager.RevokeSessionAsync(session.SessionId);

        var isValid = await _manager.ValidateSessionAsync(session.SessionId);
        isValid.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeOtherSessions_RevokesAllExceptCurrent()
    {
        var session1 = await _manager.StartSessionAsync("user-300", deviceInfo: "Laptop");
        var session2 = await _manager.StartSessionAsync("user-300", deviceInfo: "Phone");
        var session3 = await _manager.StartSessionAsync("user-300", deviceInfo: "Tablet");

        // User on Laptop (session1) logs out from all other devices:
        await _manager.RevokeOtherSessionsAsync("user-300", session1.SessionId);

        (await _manager.ValidateSessionAsync(session1.SessionId)).ShouldBeTrue();
        (await _manager.ValidateSessionAsync(session2.SessionId)).ShouldBeFalse();
        (await _manager.ValidateSessionAsync(session3.SessionId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Heartbeat_UpdatesActivity()
    {
        var session = await _manager.StartSessionAsync("user-400");
        var originalActive = session.LastActiveAt;

        await Task.Delay(10);
        await _manager.HeartbeatAsync(session.SessionId, "10.0.0.1");

        var updated = await _store.GetSessionAsync(session.SessionId);
        updated.ShouldNotBeNull();
        updated.LastActiveAt.ShouldBeGreaterThan(originalActive);
        updated.IpAddress.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void DiRegistration_AddKyrolusSessions_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusSessions();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusSessionStore>().ShouldNotBeNull();
        provider.GetService<IKyrolusSessionManager>().ShouldNotBeNull();
    }

    [Fact]
    public async Task Heartbeat_DoesNotRevive_RevokedOrExpiredSession()
    {
        var session = await _manager.StartSessionAsync("user-dead");
        await _manager.RevokeSessionAsync(session.SessionId);

        var originalActivity = session.LastActiveAt;

        // Attempting heartbeat on revoked session
        await _manager.HeartbeatAsync(session.SessionId, "1.2.3.4");

        var loaded = await _store.GetSessionAsync(session.SessionId);
        loaded.ShouldNotBeNull();
        loaded.IsRevoked.ShouldBeTrue();
        loaded.LastActiveAt.ShouldBe(originalActivity); // Unchanged!
    }

    [Fact]
    public async Task PurgeInactiveSessions_RemovesOldSessionsFromMemory()
    {
        var session1 = await _manager.StartSessionAsync("user-p1", customLifetime: TimeSpan.FromSeconds(-10));
        var session2 = await _manager.StartSessionAsync("user-p2");
        await _manager.RevokeSessionAsync(session2.SessionId);

        var purged = await _store.PurgeInactiveSessionsAsync();
        purged.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task StartSession_TruncatesExcessivelyLongUserAgent()
    {
        var hugeAgent = new string('A', 1000);
        var session = await _manager.StartSessionAsync("user-agent-test", userAgent: hugeAgent);

        session.UserAgent.ShouldNotBeNull();
        session.UserAgent.Length.ShouldBe(512);
    }

    [Fact]
    public void IsActive_RespectsClockSkewTolerance()
    {
        var session = new KyrolusUserSession
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-2),
            IsRevoked = false
        };

        // Without skew, it is expired:
        session.IsActive().ShouldBeFalse();

        // With 5 seconds skew tolerance, it is considered active:
        session.IsActive(clockSkew: TimeSpan.FromSeconds(5)).ShouldBeTrue();
    }

    [Fact]
    public async Task StartSession_RevokesOldestSession_WhenMaxActiveSessionsLimitExceeded()
    {
        var options = new KyrolusSessionOptions { MaxActiveSessionsPerUser = 2 };
        var manager = new KyrolusSessionManager(_store, options);

        var s1 = await manager.StartSessionAsync("user-limit-1");
        await Task.Delay(15);
        var s2 = await manager.StartSessionAsync("user-limit-1");
        await Task.Delay(15);
        var s3 = await manager.StartSessionAsync("user-limit-1");

        var activeSessions = await _store.GetActiveUserSessionsAsync("user-limit-1");
        activeSessions.Count.ShouldBe(2);
        activeSessions.Select(s => s.SessionId).ShouldNotContain(s1.SessionId);
        activeSessions.Select(s => s.SessionId).ShouldContain(s2.SessionId);
        activeSessions.Select(s => s.SessionId).ShouldContain(s3.SessionId);

        (await manager.ValidateSessionAsync(s1.SessionId)).ShouldBeFalse();
        (await manager.ValidateSessionAsync(s2.SessionId)).ShouldBeTrue();
        (await manager.ValidateSessionAsync(s3.SessionId)).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSessionAsync_DoesNotThrow_UnderRapidCreation()
    {
        var store = new KyrolusInMemorySessionStore();
        for (int i = 0; i < 100; i++)
        {
            await store.CreateSessionAsync(new KyrolusUserSession
            {
                SessionId = $"sess-{i}",
                UserId = "user-load",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            });
        }

        var active = await store.GetActiveUserSessionsAsync("user-load");
        active.Count.ShouldBe(100);
    }

    [Fact]
    public async Task StartSession_StripsControlCharacters_FromTelemetry()
    {
        var taintedAgent = "Mozilla/5.0\r\nInjected-Header: evil\0";
        var taintedDevice = "iPhone\n15\0Pro";

        var session = await _manager.StartSessionAsync("user-ctrl", userAgent: taintedAgent, deviceInfo: taintedDevice);

        var agent = session.UserAgent.ShouldNotBeNull();
        agent.ShouldNotContain("\r");
        agent.ShouldNotContain("\n");
        agent.ShouldNotContain("\0");

        var device = session.DeviceInfo.ShouldNotBeNull();
        device.ShouldNotContain("\n");
        device.ShouldNotContain("\0");
        device.ShouldBe("iPhone15Pro");
    }

    [Fact]
    public async Task StartSession_ClampsDefaultLifetime_ToAtLeastOneMinute()
    {
        var managerWithZero = new KyrolusSessionManager(new KyrolusInMemorySessionStore(), new KyrolusSessionOptions
        {
            DefaultSessionLifetime = TimeSpan.FromSeconds(-50)
        });

        var session = await managerWithZero.StartSessionAsync("user-clamp");
        (session.ExpiresAt - session.CreatedAt).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMinutes(1));
    }
}
