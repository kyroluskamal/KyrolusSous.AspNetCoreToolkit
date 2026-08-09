namespace KyrolusSous.Validation.Fluent.UnitTests;

public sealed class FluentValidationTests
{
    private sealed record UserRegistration(
        string Email,
        string NationalId,
        string Iban,
        string Password,
        string JsonPayload,
        string Base64Data,
        double Latitude,
        double Longitude,
        string Cron,
        string MacAddress,
        bool IsBusiness);

    private sealed class UserRegistrationValidator : KyrolusAbstractValidator<UserRegistration>
    {
        public UserRegistrationValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .WithErrorCode("ERR_EMAIL")
                .WithSeverity(KyrolusValidationSeverity.Error);

            RuleFor(x => x.NationalId)
                .NationalId("EG")
                .WithErrorCode("ERR_NATIONAL_ID");

            RuleFor(x => x.Iban)
                .IbanValid()
                .When(x => x.IsBusiness);

            RuleFor(x => x.Password).StrongPassword();
            RuleFor(x => x.JsonPayload).JsonValid();
            RuleFor(x => x.Base64Data).Base64Valid();
            RuleFor(x => x.Cron).CronExpressionValid();
            RuleFor(x => x.MacAddress).MacAddressValid();
        }
    }

    [Fact(DisplayName = "Fluent Chaining Syntax validates all properties smoothly")]
    public async Task Fluent_Chaining_Syntax_validates_properties()
    {
        var validator = new UserRegistrationValidator();
        var validUser = new UserRegistration(
            Email: "user@example.com",
            NationalId: "29812250101231",
            Iban: "GB82 WEST 1234 5698 7654 32",
            Password: "P@ssw0rd2026!",
            JsonPayload: "{\"status\":\"ok\"}",
            Base64Data: "SGVsbG8=",
            Latitude: 30.0444,
            Longitude: 31.2357,
            Cron: "0 0 * * *",
            MacAddress: "00:1A:2B:3C:4D:5E",
            IsBusiness: true);

        var failures = await validator.ValidateAsync(validUser);
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Valid Egyptian National ID succeeds validation")]
    public void Valid_Egyptian_National_ID_succeeds()
    {
        var validEgyptianId = "29812250101231"; 
        var isValid = AdvancedRuleBuilderExtensions.IsNationalIdValid(validEgyptianId, "EG");
        isValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Invalid Egyptian National ID fails validation")]
    public void Invalid_Egyptian_National_ID_fails()
    {
        var invalidId = "10000000000000";
        AdvancedRuleBuilderExtensions.IsNationalIdValid(invalidId, "EG").ShouldBeFalse();
    }

    [Fact(DisplayName = "Valid IBAN succeeds validation")]
    public void Valid_IBAN_succeeds()
    {
        var validIban = "GB82 WEST 1234 5698 7654 32";
        AdvancedRuleBuilderExtensions.IsIbanValid(validIban).ShouldBeTrue();
    }

    [Fact(DisplayName = "Invalid IBAN fails validation")]
    public void Invalid_IBAN_fails()
    {
        var invalidIban = "GB00 INVALID 0000";
        AdvancedRuleBuilderExtensions.IsIbanValid(invalidIban).ShouldBeFalse();
    }

    [Fact(DisplayName = "Strong Password validator correctly evaluates passwords")]
    public void Strong_Password_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsStrongPasswordValid("P@ssw0rd2026!").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsStrongPasswordValid("weak").ShouldBeFalse();
    }

    [Fact(DisplayName = "JSON validator correctly evaluates JSON payload")]
    public void Json_validator_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsJsonValid("{\"key\":\"value\"}").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsJsonValid("{malformed_json}").ShouldBeFalse();
    }

    [Fact(DisplayName = "Base64 validator correctly evaluates base64 strings")]
    public void Base64_validator_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsBase64Valid("SGVsbG8gV29ybGQ=").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsBase64Valid("!!!NotBase64!!!").ShouldBeFalse();
    }

    [Fact(DisplayName = "Coordinates validator correctly evaluates latitude and longitude")]
    public void Coordinates_validator_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsCoordinatesValid(30.0444, 31.2357).ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsCoordinatesValid(100.0, 31.2357).ShouldBeFalse();
    }

    [Fact(DisplayName = "Cron expression validator correctly evaluates cron syntax")]
    public void Cron_validator_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsCronExpressionValid("0 0 * * *").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsCronExpressionValid("invalid cron syntax string").ShouldBeFalse();
    }

    [Fact(DisplayName = "MAC Address validator correctly evaluates MAC formats")]
    public void Mac_Address_validator_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsMacAddressValid("00:1A:2B:3C:4D:5E").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsMacAddressValid("001A2B3C4D5E").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsMacAddressValid("INVALID_MAC").ShouldBeFalse();
    }

    [Fact(DisplayName = "AbstractValidator returns failures when invalid using fluent chaining")]
    public async Task AbstractValidator_returns_failures_when_invalid()
    {
        var validator = new UserRegistrationValidator();
        var request = new UserRegistration(
            Email: "",
            NationalId: "invalid-id",
            Iban: "invalid-iban",
            Password: "123",
            JsonPayload: "{bad-json}",
            Base64Data: "bad-base64",
            Latitude: 0,
            Longitude: 0,
            Cron: "bad-cron",
            MacAddress: "bad-mac",
            IsBusiness: true);

        var failures = await validator.ValidateAsync(request);

        failures.Count.ShouldBeGreaterThan(0);
        failures.ShouldContain(f => f.PropertyName == "Email" && f.ErrorCode == "ERR_EMAIL" && f.Severity == KyrolusValidationSeverity.Error);
        failures.ShouldContain(f => f.PropertyName == "NationalId");
    }

    [Fact(DisplayName = "AbstractValidator with CascadeMode.Stop halts on first failure")]
    public async Task AbstractValidator_with_CascadeMode_Stop_halts_on_first_failure()
    {
        var validator = new UserRegistrationValidator
        {
            CascadeMode = CascadeMode.Stop
        };

        var request = new UserRegistration(
            Email: "",
            NationalId: "invalid-id",
            Iban: "invalid-iban",
            Password: "123",
            JsonPayload: "{bad-json}",
            Base64Data: "bad-base64",
            Latitude: 0,
            Longitude: 0,
            Cron: "bad-cron",
            MacAddress: "bad-mac",
            IsBusiness: true);

        var failures = await validator.ValidateAsync(request);

        failures.Count.ShouldBe(1);
        failures[0].PropertyName.ShouldBe("Email");
    }
}
