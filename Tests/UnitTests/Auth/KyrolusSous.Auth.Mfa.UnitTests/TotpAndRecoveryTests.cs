using KyrolusSous.Auth.Mfa;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Mfa.UnitTests;

public class TotpAndRecoveryTests
{
    private readonly KyrolusTotpService _totp = new();
    private readonly KyrolusRecoveryCodeService _recovery = new();

    [Fact]
    public void GenerateSecret_ReturnsValidBase32String()
    {
        var secret = _totp.GenerateSecret();

        secret.ShouldNotBeNullOrWhiteSpace();
        secret.Length.ShouldBeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void GenerateCode_ProducesSixDigitCode()
    {
        var secret = _totp.GenerateSecret();
        var code = _totp.GenerateCode(secret);

        code.ShouldNotBeNullOrWhiteSpace();
        code.Length.ShouldBe(6);
        int.TryParse(code, out _).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_ValidatesCorrectCode()
    {
        var secret = _totp.GenerateSecret();
        var now = DateTimeOffset.UtcNow;
        var code = _totp.GenerateCode(secret, now);

        var isValid = _totp.ValidateCode(secret, code, allowedClockDriftWindows: 1, timestamp: now);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_AllowsClockDriftWindow()
    {
        var secret = _totp.GenerateSecret();
        var past = DateTimeOffset.UtcNow.AddSeconds(-30);
        var pastCode = _totp.GenerateCode(secret, past);

        // Current time validation with window=1 should accept past code from 30s ago
        var isValid = _totp.ValidateCode(secret, pastCode, allowedClockDriftWindows: 1, timestamp: DateTimeOffset.UtcNow);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_RejectsWrongCode()
    {
        var secret = _totp.GenerateSecret();
        var isValid = _totp.ValidateCode(secret, "999999");

        // Could randomly collide, but practically false unless 999999 happens to be the OTP
        var expected = _totp.GenerateCode(secret);
        if (expected != "999999")
        {
            isValid.ShouldBeFalse();
        }
    }

    [Fact]
    public void GenerateQrCodeUri_FormatsCorrectly()
    {
        var secret = "JBSWY3DPEHPK3PXP";
        var uri = _totp.GenerateQrCodeUri(secret, "alice@example.com", "MyCoolApp");

        uri.ShouldStartWith("otpauth://totp/");
        uri.ShouldContain("MyCoolApp");
        uri.ShouldContain("alice%40example.com");
        uri.ShouldContain("secret=JBSWY3DPEHPK3PXP");
        uri.ShouldContain("digits=6");
    }

    [Fact]
    public void RecoveryCodes_GenerateAndVerify_WorkCorrectly()
    {
        var codes = _recovery.GenerateRecoveryCodes(5, 10);

        codes.Count.ShouldBe(5);
        codes.Distinct().Count().ShouldBe(5);

        var targetCode = codes[0];
        var hash = _recovery.HashRecoveryCode(targetCode);

        _recovery.VerifyRecoveryCode(targetCode, hash).ShouldBeTrue();
        _recovery.VerifyRecoveryCode("WRONGCODE1", hash).ShouldBeFalse();
    }

    [Fact]
    public void GenerateRecoveryCodes_ClampsExcessiveCountAndLength()
    {
        var codes = _recovery.GenerateRecoveryCodes(count: 1000, length: 500);

        codes.Count.ShouldBe(100);
        codes[0].Length.ShouldBe(64);
    }

    [Fact]
    public void DiRegistration_AddKyrolusMfa_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMfa();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusTotpService>().ShouldNotBeNull();
        provider.GetService<IKyrolusRecoveryCodeService>().ShouldNotBeNull();
    }

    [Fact]
    public void ValidateCode_Succeeds_WhenSecretContainsTrailingEqualPadding()
    {
        // Secret with RFC 4648 padding '='
        var baseSecret = _totp.GenerateSecret(10);
        var paddedSecret = baseSecret + "==";
        var now = DateTimeOffset.UtcNow;
        var code = _totp.GenerateCode(paddedSecret, now);

        var isValid = _totp.ValidateCode(paddedSecret, code, allowedClockDriftWindows: 1, timestamp: now);
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_HandlesNegativeClockDriftGracefully()
    {
        var secret = _totp.GenerateSecret();
        var now = DateTimeOffset.UtcNow;
        var code = _totp.GenerateCode(secret, now);

        // Passing negative drift should be clamped to 0 without skipping verification
        var isValid = _totp.ValidateCode(secret, code, allowedClockDriftWindows: -2, timestamp: now);
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void VerifyRecoveryCode_NormalizesHyphensAndSpacesInUserInput()
    {
        var rawCode = "ABCDEFGH23";
        var hash = _recovery.HashRecoveryCode(rawCode);

        // User enters recovery code formatted with dashes and spaces: "ABC-DEF GH23"
        _recovery.VerifyRecoveryCode("ABC-DEF GH23", hash).ShouldBeTrue();
        _recovery.VerifyRecoveryCode(" abc-def gh23 ", hash).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_Succeeds_WithFormattedCode_And_ClampsHighDrift()
    {
        var secret = _totp.GenerateSecret();
        var now = DateTimeOffset.UtcNow;
        var code = _totp.GenerateCode(secret, now);

        var formatted = $" {code[..3]}-{code[3..]} ";
        var isValid = _totp.ValidateCode(secret, formatted, allowedClockDriftWindows: 100, timestamp: now);
        isValid.ShouldBeTrue();

        var spaceFormatted = $" {code[..3]} {code[3..]} ";
        _totp.ValidateCode(secret, spaceFormatted, timestamp: now).ShouldBeTrue();
    }

    [Fact]
    public void Totp_EnforcesMinimumEntropy_OnLowEntropySecrets()
    {
        var lowEntropySecret = "MY======";

        Should.Throw<ArgumentException>(() => _totp.GenerateCode(lowEntropySecret));
        _totp.ValidateCode(lowEntropySecret, "123456").ShouldBeFalse();
    }
}
