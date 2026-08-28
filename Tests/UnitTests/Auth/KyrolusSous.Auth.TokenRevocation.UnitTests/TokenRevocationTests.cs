using System.Security.Claims;
using KyrolusSous.Auth.TokenRevocation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace KyrolusSous.Auth.TokenRevocation.UnitTests;

public class TokenRevocationTests
{
    private readonly KyrolusInMemoryTokenBlacklist _blacklist = new();

    [Fact(DisplayName = "Revoke Token And Is Token Revoked Work Correctly")]
    public async Task RevokeToken_And_IsTokenRevoked_WorkCorrectly()
    {
        var jti = "jwt-unique-id-123";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        (await _blacklist.IsTokenRevokedAsync(jti)).ShouldBeFalse();

        await _blacklist.RevokeTokenAsync(jti, expiresAt);

        (await _blacklist.IsTokenRevokedAsync(jti)).ShouldBeTrue();
    }

    [Fact(DisplayName = "Expired Revoked Token Cleans Up Automatically")]
    public async Task ExpiredRevokedToken_CleansUpAutomatically()
    {
        var jti = "expired-token-jti";
        var pastExpiry = DateTimeOffset.UtcNow.AddSeconds(-5);

        await _blacklist.RevokeTokenAsync(jti, pastExpiry);

        // Since expiresAt is in the past, checking it will clean it up and return false
        (await _blacklist.IsTokenRevokedAsync(jti)).ShouldBeFalse();
    }

    [Fact(DisplayName = "User Revocation Revokes Older Tokens And Allows Newer")]
    public async Task UserRevocation_RevokesOlderTokens_And_AllowsNewer()
    {
        var userId = "user-multi-device";
        var cutoff = DateTimeOffset.UtcNow;

        var oldTokenIssuedAt = cutoff.AddMinutes(-5);
        var newTokenIssuedAt = cutoff.AddMinutes(5);

        await _blacklist.RevokeUserTokensAsync(userId, cutoff);

        // Older token should be revoked:
        (await _blacklist.IsUserTokenRevokedAsync(userId, oldTokenIssuedAt)).ShouldBeTrue();

        // Newer token issued after the cutoff should be valid:
        (await _blacklist.IsUserTokenRevokedAsync(userId, newTokenIssuedAt)).ShouldBeFalse();
    }

    [Fact(DisplayName = "Validator Rejects Principal With Revoked Jti")]
    public async Task Validator_RejectsPrincipal_WithRevokedJti()
    {
        var jti = "revoked-principal-jti";
        await _blacklist.RevokeTokenAsync(jti, DateTimeOffset.UtcNow.AddHours(1));

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("jti", jti),
            new Claim("sub", "user-123")
        ], "Bearer"));

        var isValid = await KyrolusTokenRevocationValidator.IsValidAsync(principal, _blacklist);
        isValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Validator Accepts Valid Principal")]
    public async Task Validator_AcceptsValidPrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("jti", "valid-unrevoked-jti"),
            new Claim("sub", "user-456")
        ], "Bearer"));

        var isValid = await KyrolusTokenRevocationValidator.IsValidAsync(principal, _blacklist);
        isValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Di Registration Add Kyrolus Token Revocation Registers Service")]
    public void DiRegistration_AddKyrolusTokenRevocation_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddKyrolusTokenRevocation();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusTokenBlacklist>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "Purge Expired Revocations Cleans Up Unqueried Jtis")]
    public async Task PurgeExpiredRevocations_CleansUpUnqueriedJtis()
    {
        // Add revocation that expires shortly
        await _blacklist.RevokeTokenAsync("unqueried-expired-jti", DateTimeOffset.UtcNow.AddMilliseconds(50));
        await Task.Delay(100);

        var count = await _blacklist.PurgeExpiredRevocationsAsync();
        count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "Is Valid Async Correctly Parses Various Iat Formats")]
    public async Task IsValidAsync_CorrectlyParsesVariousIatFormats()
    {
        var userId = "user-formats";
        var cutoff = DateTimeOffset.UtcNow;
        await _blacklist.RevokeUserTokensAsync(userId, cutoff);

        // 1. Integer epoch seconds older than cutoff -> revoked
        var oldEpochSeconds = cutoff.AddMinutes(-5).ToUnixTimeSeconds().ToString();
        var p1 = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId), new Claim("iat", oldEpochSeconds)], "Bearer"));
        (await KyrolusTokenRevocationValidator.IsValidAsync(p1, _blacklist)).ShouldBeFalse();

        // 2. Float epoch seconds
        var oldEpochFloat = (cutoff.AddMinutes(-5).ToUnixTimeMilliseconds() / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var p2 = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId), new Claim("iat", oldEpochFloat)], "Bearer"));
        (await KyrolusTokenRevocationValidator.IsValidAsync(p2, _blacklist)).ShouldBeFalse();

        // 3. ISO8601 string
        var oldIso = cutoff.AddMinutes(-5).ToString("O");
        var p3 = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId), new Claim("iat", oldIso)], "Bearer"));
        (await KyrolusTokenRevocationValidator.IsValidAsync(p3, _blacklist)).ShouldBeFalse();
    }

    [Fact(DisplayName = "Is Valid Async Works For Principals Without Jti")]
    public async Task IsValidAsync_WorksForPrincipalsWithoutJti()
    {
        var principalWithoutJti = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "user-no-jti")
        ], "Bearer"));

        var isValid = await KyrolusTokenRevocationValidator.IsValidAsync(principalWithoutJti, _blacklist);
        isValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Revoke User Tokens Maintains Monotonic Latest Cutoff")]
    public async Task RevokeUserTokens_MaintainsMonotonicLatestCutoff()
    {
        var userId = "user-monotonic";
        var now = DateTimeOffset.UtcNow;
        var newer = now.AddMinutes(10);
        var older = now.AddMinutes(5);

        await _blacklist.RevokeUserTokensAsync(userId, newer);
        // Submitting an older revocation should NOT roll back the cutoff!
        await _blacklist.RevokeUserTokensAsync(userId, older);

        // Token issued at now.AddMinutes(7) should be revoked because newer is now.AddMinutes(10)
        var isRevoked = await _blacklist.IsUserTokenRevokedAsync(userId, now.AddMinutes(7));
        isRevoked.ShouldBeTrue();
    }

    [Fact(DisplayName = "Revoke Token Throws On Whitespace Jti")]
    public async Task RevokeToken_Throws_OnWhitespaceJti()
    {
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _blacklist.RevokeTokenAsync("   ", DateTimeOffset.UtcNow.AddMinutes(1)));
    }

    [Fact(DisplayName = "Is User Token Revoked Handles Sub Second Precision Correctly")]
    public async Task IsUserTokenRevoked_HandlesSubSecondPrecisionCorrectly()
    {
        var userId = "user-subsecond";
        var now = DateTimeOffset.UtcNow;
        var tokenIat = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var cutoff = tokenIat.AddMilliseconds(500);

        await _blacklist.RevokeUserTokensAsync(userId, cutoff);

        var isRevoked = await _blacklist.IsUserTokenRevokedAsync(userId, tokenIat);
        isRevoked.ShouldBeTrue();
    }

    [Fact(DisplayName = "Is Valid Async Returns True For Unauthenticated Principal")]
    public async Task IsValidAsync_ReturnsTrue_ForUnauthenticatedPrincipal()
    {
        var unauth = new ClaimsPrincipal(new ClaimsIdentity());
        var isValid = await KyrolusTokenRevocationValidator.IsValidAsync(unauth, _blacklist);
        isValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Revoke Token Async Ignores Tokens Already Expired")]
    public async Task RevokeTokenAsync_IgnoresTokens_AlreadyExpired()
    {
        var expiredJti = "already-expired-jti";
        var pastExpiry = DateTimeOffset.UtcNow.AddMinutes(-5);

        await _blacklist.RevokeTokenAsync(expiredJti, pastExpiry);

        var isRevoked = await _blacklist.IsTokenRevokedAsync(expiredJti);
        isRevoked.ShouldBeFalse();
    }

    [Fact(DisplayName = "Cache Token Blacklist Revokes And Validates Correctly")]
    public async Task CacheTokenBlacklist_RevokesAndValidates_Correctly()
    {
        var cache = NSubstitute.Substitute.For<KyrolusSous.Caching.Abstractions.IKyrolusCacheProvider>();
        var blacklist = new KyrolusCacheTokenBlacklist(cache);

        var jti = "distributed-jti-123";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        cache.GetAsync<long?>($"auth:revoked:jti:{jti}", Arg.Any<CancellationToken>())
            .Returns(expiresAt.ToUnixTimeSeconds());

        var isRevoked = await blacklist.IsTokenRevokedAsync(jti);
        isRevoked.ShouldBeTrue();

        await blacklist.RevokeTokenAsync(jti, expiresAt);
        await cache.Received(1).SetAsync($"auth:revoked:jti:{jti}", expiresAt.ToUnixTimeSeconds(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cache Token Blacklist Revokes User Tokens Correctly")]
    public async Task CacheTokenBlacklist_RevokesUserTokens_Correctly()
    {
        var cache = NSubstitute.Substitute.For<KyrolusSous.Caching.Abstractions.IKyrolusCacheProvider>();
        var blacklist = new KyrolusCacheTokenBlacklist(cache);

        var userId = "user-dist-999";
        var cutoff = DateTimeOffset.UtcNow;

        cache.GetAsync<long?>($"auth:revoked:user:{userId}", Arg.Any<CancellationToken>())
            .Returns(cutoff.ToUnixTimeSeconds());

        var isRevoked = await blacklist.IsUserTokenRevokedAsync(userId, cutoff.AddMinutes(-1));
        isRevoked.ShouldBeTrue();

        var isAllowed = await blacklist.IsUserTokenRevokedAsync(userId, cutoff.AddMinutes(1));
        isAllowed.ShouldBeFalse();
    }
}
