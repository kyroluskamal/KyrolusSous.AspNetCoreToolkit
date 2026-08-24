using KyrolusSous.Logging.Serilog.Destructuring;
using Serilog.Core;
using Serilog.Events;

namespace KyrolusSous.Logging.UnitTests;

public sealed class MaskingTests
{
    private sealed class UserAccountDto
    {
        public string Username { get; set; } = "john_doe";

        public string Password { get; set; } = "SuperSecretP@ss123";

        public string ApiKey { get; set; } = "ak_live_abcdef123456";

        [KyrolusMasked(ShowFirst = 4, ShowLast = 4, PreserveLength = true)]
        public string CreditCardNumber { get; set; } = "4111111111111234";

        [KyrolusMasked(ShowLast = 4)]
        public string NationalId { get; set; } = "29901011234567";

        [KyrolusMasked]
        public string SecurityAnswer { get; set; } = "MyFirstPet";
    }

    [Fact(DisplayName = "KyrolusSensitiveDataMasker: Detects standard sensitive keywords")]
    public void SensitiveDataMasker_DetectsSensitiveKeywords()
    {
        var masker = new KyrolusSensitiveDataMasker();

        masker.IsSensitivePropertyName("password").ShouldBeTrue();
        masker.IsSensitivePropertyName("UserPassword").ShouldBeTrue();
        masker.IsSensitivePropertyName("api_key").ShouldBeTrue();
        masker.IsSensitivePropertyName("Authorization").ShouldBeTrue();
        masker.IsSensitivePropertyName("creditCard").ShouldBeTrue();
        masker.IsSensitivePropertyName("cvv").ShouldBeTrue();
        masker.IsSensitivePropertyName("ssn").ShouldBeTrue();
        masker.IsSensitivePropertyName("Username").ShouldBeFalse();
        masker.IsSensitivePropertyName("Email").ShouldBeFalse();
        masker.IsSensitivePropertyName("Author").ShouldBeFalse();
        masker.IsSensitivePropertyName("Keyboard").ShouldBeFalse();
        masker.IsSensitivePropertyName("Monkey").ShouldBeFalse();
        masker.IsSensitivePropertyName(string.Empty).ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusSensitiveDataMasker: Custom keywords support")]
    public void SensitiveDataMasker_SupportsCustomKeywords()
    {
        var masker = new KyrolusSensitiveDataMasker(["CustomSecretProp", "BiometricHash"]);

        masker.IsSensitivePropertyName("CustomSecretProp").ShouldBeTrue();
        masker.IsSensitivePropertyName("UserBiometricHash").ShouldBeTrue();
        masker.IsSensitivePropertyName("NormalProp").ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusSensitiveDataMasker: MaskString rules and partial unmasking")]
    public void SensitiveDataMasker_MaskString_Variants()
    {
        var masker = new KyrolusSensitiveDataMasker();

        // Default mask
        masker.MaskString("Secret123").ShouldBe("***MASKED***");
        masker.MaskString(null).ShouldBe(string.Empty);
        masker.MaskString(string.Empty).ShouldBe(string.Empty);

        // ShowFirst and ShowLast with PreserveLength
        var ccRule = new KyrolusMaskedAttribute { ShowFirst = 4, ShowLast = 4, PreserveLength = true };
        var maskedCC = masker.MaskString("4111111111111234", ccRule);
        maskedCC.ShouldBe("4111********1234");
        maskedCC.Length.ShouldBe(16);

        // ShowLast only without PreserveLength
        var lastOnlyRule = new KyrolusMaskedAttribute { ShowLast = 4 };
        var maskedLast = masker.MaskString("1234567890", lastOnlyRule);
        maskedLast.ShouldBe("****7890");

        // Overlapping visible counts
        var smallStringRule = new KyrolusMaskedAttribute { ShowFirst = 5, ShowLast = 5 };
        var maskedSmall = masker.MaskString("abc", smallStringRule);
        maskedSmall.ShouldBe("***");
    }

    [Fact(DisplayName = "KyrolusSensitiveDataMasker: SanitizeProperties dictionary masking")]
    public void SensitiveDataMasker_SanitizeProperties_MasksCorrectly()
    {
        var masker = new KyrolusSensitiveDataMasker();
        var props = new Dictionary<string, object?>
        {
            ["Username"] = "alice",
            ["Password"] = "P@ssword1",
            ["CardNumber"] = "1234567890123456",
            ["Age"] = 30
        };

        var sanitized = masker.SanitizeProperties(props);
        sanitized["Username"].ShouldBe("alice");
        sanitized["Password"].ShouldBe("***MASKED***");
        sanitized["CardNumber"].ShouldBe("***MASKED***");
        sanitized["Age"].ShouldBe(30);

        masker.SanitizeProperties(null).ShouldBeEmpty();
    }

    [Fact(DisplayName = "KyrolusSerilogDestructuringPolicy: Masks object properties automatically")]
    public void SerilogDestructuringPolicy_MasksObjectProperties()
    {
        var policy = new KyrolusSerilogDestructuringPolicy();
        var factory = new MockPropertyValueFactory();

        var user = new UserAccountDto();
        var success = policy.TryDestructure(user, factory, out var result);

        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.ShouldBeOfType<StructureValue>();

        var structVal = (StructureValue)result;
        var passProp = structVal.Properties.FirstOrDefault(p => p.Name == "Password");
        passProp.ShouldNotBeNull();
        passProp.Value.ToString().ShouldContain("***MASKED***");

        var ccProp = structVal.Properties.FirstOrDefault(p => p.Name == "CreditCardNumber");
        ccProp.ShouldNotBeNull();
        ccProp.Value.ToString().ShouldContain("4111********1234");
    }

    private sealed class MockPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
        {
            return new ScalarValue(value);
        }
    }
}
