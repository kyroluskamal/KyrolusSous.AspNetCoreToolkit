using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Tokens.UnitTests;

public class UserTokenServiceTests
{
    private readonly KyrolusUserTokenService _service = new();

    private readonly KyrolusAuthUser _user = new()
    {
        Id = "user-token-123",
        UserName = "kyrolus",
        Email = "kyrolus@example.com",
        SecurityStamp = "initial-security-stamp-guid"
    };

    [Fact(DisplayName = "Generate Token And Validate Token Work For Email Confirmation")]
    public void GenerateToken_And_ValidateToken_WorkForEmailConfirmation()
    {
        var token = _service.GenerateToken(_user, KyrolusTokenPurposes.EmailConfirmation);

        token.ShouldNotBeNullOrWhiteSpace();

        var isValid = _service.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, token);
        isValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Validate Token Fails When Purpose Mismatches")]
    public void ValidateToken_Fails_WhenPurposeMismatches()
    {
        var emailToken = _service.GenerateToken(_user, KyrolusTokenPurposes.EmailConfirmation);

        // Attempting to use email confirmation token as password reset
        var isValid = _service.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, emailToken);
        isValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Validate Token Fails When User Security Stamp Changed")]
    public void ValidateToken_Fails_WhenUserSecurityStampChanged()
    {
        var token = _service.GenerateToken(_user, KyrolusTokenPurposes.PasswordReset);

        // User changed password or security stamp was refreshed:
        _user.SecurityStamp = "new-bumped-stamp-999";

        var isValid = _service.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, token);
        isValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Validate Token Fails When Token Expired")]
    public void ValidateToken_Fails_WhenTokenExpired()
    {
        // Negative lifetime -> instantly expired
        var token = _service.GenerateToken(_user, KyrolusTokenPurposes.PasswordReset, TimeSpan.FromSeconds(-10));

        var isValid = _service.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, token);
        isValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Validate Token Fails When Signature Tampered")]
    public void ValidateToken_Fails_WhenSignatureTampered()
    {
        var token = _service.GenerateToken(_user, KyrolusTokenPurposes.EmailConfirmation);
        var tampered = token[..^4] + "AAAA";

        var isValid = _service.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, tampered);
        isValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Validate Token Succeeds When User Id Or Purpose Contains Pipes")]
    public void ValidateToken_Succeeds_WhenUserIdOrPurposeContainsPipes()
    {
        var complexUser = new KyrolusAuthUser
        {
            Id = "user|tenant|123",
            SecurityStamp = "valid-stamp"
        };

        var purposeWithPipes = "Email|Confirmation|Urgent";
        var token = _service.GenerateToken(complexUser, purposeWithPipes);

        token.ShouldNotBeNullOrWhiteSpace();

        var isValid = _service.ValidateToken(complexUser, purposeWithPipes, token);
        isValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Validate Token Returns False On Malformed Base64 Payload Or Signature")]
    public void ValidateToken_ReturnsFalse_OnMalformedBase64PayloadOrSignature()
    {
        _service.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, "invalid-token-without-dots").ShouldBeFalse();
        _service.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, "malformed!base64.signature").ShouldBeFalse();
        _service.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, "payload.malformed!signature").ShouldBeFalse();
    }

    [Fact(DisplayName = "Di Registration Add Kyrolus User Tokens Registers Service")]
    public void DiRegistration_AddKyrolusUserTokens_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddKyrolusUserTokens();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusUserTokenService>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "Validate Token Fails Fast When Token Segments Have Modulo One Length")]
    public void ValidateToken_FailsFast_WhenTokenSegmentsHaveModuloOneLength()
    {
        // A single character segment has length % 4 == 1, which is mathematically impossible in Base64
        var invalidModuloOneToken = "A.valid";
        var result = _service.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, invalidModuloOneToken);
        result.ShouldBeFalse();
    }

    [Fact(DisplayName = "Validate Token Succeeds Within Clock Skew Tolerance")]
    public void ValidateToken_Succeeds_WithinClockSkewTolerance()
    {
        var options = new KyrolusUserTokenOptions
        {
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        var serviceWithSkew = new KyrolusUserTokenService(options);
        var expiredToken = serviceWithSkew.GenerateToken(_user, KyrolusTokenPurposes.PasswordReset, TimeSpan.FromSeconds(-10));

        var isValid = serviceWithSkew.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, expiredToken);
        isValid.ShouldBeTrue();

        var farExpiredToken = serviceWithSkew.GenerateToken(_user, KyrolusTokenPurposes.PasswordReset, TimeSpan.FromSeconds(-120));
        var isFarValid = serviceWithSkew.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, farExpiredToken);
        isFarValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Generate Token Throws When User Id Is Null Or Whitespace")]
    public void GenerateToken_Throws_WhenUserIdIsNullOrWhitespace()
    {
        var invalidUser = new KyrolusAuthUser { Id = "   " };
        Should.Throw<ArgumentException>(() =>
            _service.GenerateToken(invalidUser, KyrolusTokenPurposes.EmailConfirmation));
    }

    [Fact(DisplayName = "Validate Token Returns False When Expiry Is Zero Or Negative")]
    public void ValidateToken_ReturnsFalse_WhenExpiryIsZeroOrNegative()
    {
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes($"{_user.Id}|-100|{KyrolusTokenPurposes.PasswordReset}|{_user.SecurityStamp}");
        var payloadB64 = Convert.ToBase64String(payloadBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(new KyrolusUserTokenOptions().SecretKey));
        var sig = hmac.ComputeHash(payloadBytes);
        var sigB64 = Convert.ToBase64String(sig).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var token = $"{payloadB64}.{sigB64}";
        var isValid = _service.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, token);
        isValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "Data Protection User Token Service Generates And Validates Token Correctly")]
    public void DataProtectionUserTokenService_GeneratesAndValidatesToken_Correctly()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var dataProtectionProvider = sp.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();

        var dpService = new KyrolusDataProtectionUserTokenService(dataProtectionProvider);

        var token = dpService.GenerateToken(_user, KyrolusTokenPurposes.EmailConfirmation);
        token.ShouldNotBeNullOrWhiteSpace();

        var isValid = dpService.ValidateToken(_user, KyrolusTokenPurposes.EmailConfirmation, token);
        isValid.ShouldBeTrue();

        var isPurposeMismatch = dpService.ValidateToken(_user, KyrolusTokenPurposes.PasswordReset, token);
        isPurposeMismatch.ShouldBeFalse();
    }
}
