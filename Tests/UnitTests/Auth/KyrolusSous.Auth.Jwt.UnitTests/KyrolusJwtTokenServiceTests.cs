using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Jwt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KyrolusSous.Auth.Jwt.UnitTests;

public class KyrolusJwtTokenServiceTests
{
    private readonly KyrolusJwtOptions _options = new()
    {
        SecretKey = "super-secret-key-that-is-at-least-32-chars-long!",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(7),
        ClockSkew = TimeSpan.Zero
    };

    [Fact(DisplayName = "Constructor Throws When Key Too Short")]
    public void Constructor_Throws_WhenKeyTooShort()
    {
        var invalidOptions = new KyrolusJwtOptions { SecretKey = "too-short" };
        Should.Throw<ArgumentException>(() => new KyrolusJwtTokenService(invalidOptions));
    }

    [Fact(DisplayName = "Generate Access Token Produces Valid Jwt")]
    public void GenerateAccessToken_ProducesValidJwt()
    {
        var service = new KyrolusJwtTokenService(_options);
        var user = new KyrolusAuthUser
        {
            Id = "user-123",
            UserName = "kyrolus",
            Email = "kyrolus@example.com",
            EmailConfirmed = true,
            DisplayName = "Kyrolus Sous",
            Roles = ["Admin", "Manager"]
        };

        var token = service.GenerateAccessToken(user);

        token.ShouldNotBeNullOrWhiteSpace();

        var principal = service.ValidateAccessToken(token);
        principal.ShouldNotBeNull();
        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.ShouldBe("user-123");
        principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value.ShouldBe("kyrolus@example.com");
        principal.IsInRole("Admin").ShouldBeTrue();
        principal.IsInRole("Manager").ShouldBeTrue();
    }

    [Fact(DisplayName = "Generate Refresh Token Generates Unique Tokens")]
    public void GenerateRefreshToken_GeneratesUniqueTokens()
    {
        var service = new KyrolusJwtTokenService(_options);
        var token1 = service.GenerateRefreshToken();
        var token2 = service.GenerateRefreshToken();

        token1.ShouldNotBeNullOrWhiteSpace();
        token2.ShouldNotBeNullOrWhiteSpace();
        token1.ShouldNotBe(token2);
    }

    [Fact(DisplayName = "Refresh Token Hash And Verify Work Correctly")]
    public void RefreshToken_HashAndVerify_WorkCorrectly()
    {
        var service = new KyrolusJwtTokenService(_options);
        var rawToken = service.GenerateRefreshToken();

        var hash = service.HashRefreshToken(rawToken);
        hash.ShouldNotBeNullOrWhiteSpace();

        service.VerifyRefreshToken(rawToken, hash).ShouldBeTrue();
        service.VerifyRefreshToken("wrong-token", hash).ShouldBeFalse();
        service.VerifyRefreshToken(rawToken, "wrong-hash").ShouldBeFalse();
    }

    [Fact(DisplayName = "Validate Access Token Returns Null When Token Invalid")]
    public void ValidateAccessToken_ReturnsNull_WhenTokenInvalid()
    {
        var service = new KyrolusJwtTokenService(_options);
        var result = service.ValidateAccessToken("invalid.jwt.token");

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "Di Registration Add Kyrolus Jwt Token Service Registers Service")]
    public void DiRegistration_AddKyrolusJwtTokenService_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddKyrolusJwtTokenService(options =>
        {
            options.SecretKey = "another-super-secret-key-32-chars-long!!";
        });

        var provider = services.BuildServiceProvider();
        var jwtService = provider.GetService<IKyrolusJwtTokenService>();
        jwtService.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Verify Refresh Token Succeeds Regardless Of Stored Hash Casing")]
    public void VerifyRefreshToken_Succeeds_RegardlessOfStoredHashCasing()
    {
        var service = new KyrolusJwtTokenService(_options);
        var raw = service.GenerateRefreshToken();
        var upperHash = service.HashRefreshToken(raw).ToUpperInvariant();
        var lowerHash = upperHash.ToLowerInvariant();

        service.VerifyRefreshToken(raw, upperHash).ShouldBeTrue();
        service.VerifyRefreshToken(raw, lowerHash).ShouldBeTrue();
    }

    [Fact(DisplayName = "Generate Access Token Deduplicates Core Claims When Present In Additional Claims")]
    public void GenerateAccessToken_DeduplicatesCoreClaims_WhenPresentInAdditionalClaims()
    {
        var service = new KyrolusJwtTokenService(_options);
        var user = new KyrolusAuthUser { Id = "user-dedup" };

        var additional = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "malicious-replacement"),
            new("custom_claim", "custom_value")
        };

        var token = service.GenerateAccessToken(user, additional);
        var principal = service.ValidateAccessToken(token);

        principal.ShouldNotBeNull();
        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.ShouldBe("user-dedup");
        principal.FindFirst("custom_claim")?.Value.ShouldBe("custom_value");
    }

    [Fact(DisplayName = "Generate Access Token Preserves Multi Valued Claims Such As Roles And Permissions")]
    public void GenerateAccessToken_PreservesMultiValuedClaims_SuchAsRolesAndPermissions()
    {
        var service = new KyrolusJwtTokenService(_options);
        var user = new KyrolusAuthUser
        {
            Id = "user-multi",
            Roles = ["BaseRole"]
        };

        var additional = new List<Claim>
        {
            new(ClaimTypes.Role, "AdminRole"),
            new(ClaimTypes.Role, "SuperAdminRole"),
            new("permission", "users.read"),
            new("permission", "users.write")
        };

        var token = service.GenerateAccessToken(user, additional);
        var principal = service.ValidateAccessToken(token);

        principal.ShouldNotBeNull();
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        roles.ShouldContain("BaseRole");
        roles.ShouldContain("AdminRole");
        roles.ShouldContain("SuperAdminRole");

        var permissions = principal.FindAll("permission").Select(c => c.Value).ToList();
        permissions.ShouldContain("users.read");
        permissions.ShouldContain("users.write");
    }

    [Fact(DisplayName = "Validate Access Token Async Validates Token Without Blocking")]
    public async Task ValidateAccessTokenAsync_ValidatesTokenWithoutBlocking()
    {
        var service = new KyrolusJwtTokenService(_options);
        var user = new KyrolusAuthUser { Id = "user-async-jwt" };

        var token = service.GenerateAccessToken(user);
        var principal = await service.ValidateAccessTokenAsync(token);

        principal.ShouldNotBeNull();
        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.ShouldBe("user-async-jwt");
    }

    [Fact(DisplayName = "Validate Access Token Strips Bearer Prefix And Validates Successfully")]
    public void ValidateAccessToken_StripsBearerPrefix_AndValidatesSuccessfully()
    {
        var service = new KyrolusJwtTokenService(_options);
        var user = new KyrolusAuthUser { Id = "user-bearer-prefix" };
        var token = service.GenerateAccessToken(user);

        var principal = service.ValidateAccessToken($"Bearer {token}");
        principal.ShouldNotBeNull();
        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.ShouldBe("user-bearer-prefix");
    }

    [Fact(DisplayName = "Validate Access Token Rejects Oversized Tokens")]
    public void ValidateAccessToken_RejectsOversizedTokens()
    {
        var service = new KyrolusJwtTokenService(_options);
        var giantToken = new string('A', 10_000);
        service.ValidateAccessToken(giantToken).ShouldBeNull();
    }

    [Fact(DisplayName = "Generate Access Token Throws When User Id Is Null Or Whitespace")]
    public void GenerateAccessToken_Throws_WhenUserIdIsNullOrWhitespace()
    {
        var service = new KyrolusJwtTokenService(_options);
        var user = new KyrolusAuthUser { Id = "   " };

        Should.Throw<ArgumentException>(() =>
            service.GenerateAccessToken(user));
    }

    [Fact(DisplayName = "Verify Refresh Token Returns False For Oversized Inputs")]
    public void VerifyRefreshToken_ReturnsFalse_ForOversizedInputs()
    {
        var service = new KyrolusJwtTokenService(_options);
        var giantRefreshToken = new string('a', 3000);
        var giantHash = new string('b', 500);

        service.VerifyRefreshToken(giantRefreshToken, "somehash").ShouldBeFalse();
        service.VerifyRefreshToken("sometoken", giantHash).ShouldBeFalse();
    }
}
