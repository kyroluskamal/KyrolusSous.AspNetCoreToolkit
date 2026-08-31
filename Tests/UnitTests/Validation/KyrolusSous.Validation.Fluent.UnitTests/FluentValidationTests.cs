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

    [Fact(DisplayName = "Chained WithMessage keeps distinct messages per rule step")]
    public async Task Chained_WithMessage_keeps_distinct_messages_per_rule()
    {
        var validator = new ChainedMessageValidator();
        
        // Empty email should trigger NotEmpty custom message, NOT EmailAddress message
        var emptyEmailResult = await validator.ValidateAsync(new ChainedTarget(""));
        emptyEmailResult.Count.ShouldBe(1);
        emptyEmailResult[0].ErrorMessage.ShouldBe("Email is required.");

        // Non-empty but invalid format should trigger EmailAddress custom message
        var invalidEmailResult = await validator.ValidateAsync(new ChainedTarget("not-an-email"));
        invalidEmailResult.Count.ShouldBe(1);
        invalidEmailResult[0].ErrorMessage.ShouldBe("Email format is invalid.");
    }

    private sealed record ChainedTarget(string Email);
    private sealed class ChainedMessageValidator : KyrolusAbstractValidator<ChainedTarget>
    {
        public ChainedMessageValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Email format is invalid.");
        }
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

    [Fact(DisplayName = "Base64 validator correctly evaluates base64 strings and large inputs")]
    public void Base64_validator_evaluates_correctly()
    {
        AdvancedRuleBuilderExtensions.IsBase64Valid("SGVsbG8gV29ybGQ=").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsBase64Valid("!!!NotBase64!!!").ShouldBeFalse();

        // Large Base64 string test (exercises ArrayPool path)
        var largeBytes = new byte[1024];
        new Random(42).NextBytes(largeBytes);
        var largeBase64 = Convert.ToBase64String(largeBytes);
        AdvancedRuleBuilderExtensions.IsBase64Valid(largeBase64).ShouldBeTrue();
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

    [Fact(DisplayName = "AbstractValidator with KyrolusCascadeMode.Stop halts on first failure")]
    public async Task AbstractValidator_with_CascadeMode_Stop_halts_on_first_failure()
    {
        var validator = new UserRegistrationValidator
        {
            KyrolusCascadeMode = KyrolusCascadeMode.Stop
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

    private sealed record Address(string City);
    private sealed record Customer(string Name, Address Address, int Age, string Role);

    private sealed class AddressValidator : KyrolusAbstractValidator<Address>
    {
        public AddressValidator()
        {
            RuleFor(x => x.City).NotEmpty();
        }
    }

    private sealed class CustomerValidator : KyrolusAbstractValidator<Customer>
    {
        public CustomerValidator()
        {
            RuleFor(x => x.Name).NotNull().NotEqual("BannedUser");
            RuleFor(x => x.Address).SetValidator(new AddressValidator());
            RuleFor(x => x.Age).ExclusiveBetween(17, 100);
            RuleFor(x => x.Role).Equal("Admin");
            RuleFor(x => x.Name).MustAsync(async (name, ct) =>
            {
                await Task.Delay(1, ct);
                return name != "AsyncBanned";
            }, "Name is async banned.");
        }
    }

    private enum UserStatus { Active, Inactive }

    private sealed record Account(string Username, UserStatus Status, decimal Balance);

    private sealed class AccountValidator : KyrolusAbstractValidator<Account>
    {
        public AccountValidator()
        {
            RuleFor(x => x.Username).MinLength(3).MaxLength(20);
            RuleFor(x => x.Status).IsInEnum();
            RuleFor(x => x.Balance).ScalePrecision(10, 2);
        }
    }

    [Fact(DisplayName = "MinLength, MaxLength, IsInEnum, ScalePrecision execute cleanly")]
    public async Task Enum_and_ScalePrecision_validators_execute_cleanly()
    {
        var validator = new AccountValidator();
        var valid = new Account("Kyrolus", UserStatus.Active, 100.50m);
        var validFailures = await validator.ValidateAsync(valid);
        validFailures.ShouldBeEmpty();

        var invalid = new Account("ab", (UserStatus)999, 123456789012.345m);
        var invalidFailures = await validator.ValidateAsync(invalid);
        invalidFailures.Count.ShouldBeGreaterThan(0);
    }

    #region Spanish DNI, NIE, CIF, and NIF Tests
    [Theory(DisplayName = "Valid Spanish DNI numbers should pass validation")]
    [InlineData("12345678Z")]
    [InlineData("12345678-Z")]
    [InlineData("12345678 z")]
    [InlineData("00000000T")]
    [InlineData("11111111H")]
    [InlineData("53026359C")]
    public void Valid_Spanish_Dni_succeeds(string dni)
    {
        AdvancedRuleBuilderExtensions.IsSpanishDniValid(dni).ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsNationalIdValid(dni, "ES-DNI").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsNationalIdValid(dni, "ES").ShouldBeTrue();
    }

    [Theory(DisplayName = "Invalid Spanish DNI numbers should fail validation")]
    [InlineData("12345678A")] // Wrong letter
    [InlineData("1234567")]   // Too short
    [InlineData("123456789")] // No letter
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_Spanish_Dni_fails(string? dni)
    {
        AdvancedRuleBuilderExtensions.IsSpanishDniValid(dni).ShouldBeFalse();
    }

    [Theory(DisplayName = "Valid Spanish NIE numbers should pass validation")]
    [InlineData("X1234567L")]
    [InlineData("X-1234567-L")]
    [InlineData("x 1234567 l")]
    [InlineData("Y1234567X")]
    [InlineData("Z1234567R")]
    public void Valid_Spanish_Nie_succeeds(string nie)
    {
        AdvancedRuleBuilderExtensions.IsSpanishNieValid(nie).ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsNationalIdValid(nie, "ES-NIE").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsNationalIdValid(nie, "ES").ShouldBeTrue();
    }

    [Theory(DisplayName = "Invalid Spanish NIE numbers should fail validation")]
    [InlineData("A1234567L")] // Invalid prefix
    [InlineData("X1234567A")] // Wrong control letter
    [InlineData("X123456")]   // Too short
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_Spanish_Nie_fails(string? nie)
    {
        AdvancedRuleBuilderExtensions.IsSpanishNieValid(nie).ShouldBeFalse();
    }

    [Theory(DisplayName = "Valid Spanish CIF numbers should pass validation")]
    [InlineData("A58818501")]
    [InlineData("A-5881850-1")]
    [InlineData("P5881850A")]
    public void Valid_Spanish_Cif_succeeds(string cif)
    {
        AdvancedRuleBuilderExtensions.IsSpanishCifValid(cif).ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsNationalIdValid(cif, "ES-CIF").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsNationalIdValid(cif, "ES").ShouldBeTrue();
    }

    [Theory(DisplayName = "Invalid Spanish CIF numbers should fail validation")]
    [InlineData("Z58818501")] // Invalid prefix for CIF
    [InlineData("A58818502")] // Wrong control digit
    [InlineData("P58818501")] // 'P' requires letter control
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_Spanish_Cif_fails(string? cif)
    {
        AdvancedRuleBuilderExtensions.IsSpanishCifValid(cif).ShouldBeFalse();
    }

    [Fact(DisplayName = "Spanish NIF validator accepts valid DNI, NIE, and CIF")]
    public void Spanish_Nif_accepts_valid_Dni_Nie_and_Cif()
    {
        AdvancedRuleBuilderExtensions.IsSpanishNifValid("12345678Z").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsSpanishNifValid("X1234567L").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsSpanishNifValid("A58818501").ShouldBeTrue();
        AdvancedRuleBuilderExtensions.IsSpanishNifValid("invalid-nif").ShouldBeFalse();
    }

    private sealed record SpanishDocumentHolder(string? Dni, string? Nie, string? Cif, string? Nif);

    private sealed class SpanishDocumentHolderValidator : KyrolusAbstractValidator<SpanishDocumentHolder>
    {
        public SpanishDocumentHolderValidator()
        {
            RuleFor(x => x.Dni).SpanishDni();
            RuleFor(x => x.Nie).SpanishNie();
            RuleFor(x => x.Cif).SpanishCif();
            RuleFor(x => x.Nif).SpanishNif();
        }
    }

    [Fact(DisplayName = "Spanish fluent validator extensions execute correctly in abstract validator")]
    public async Task Spanish_fluent_validator_extensions_execute_correctly()
    {
        var validator = new SpanishDocumentHolderValidator();

        var valid = new SpanishDocumentHolder("12345678Z", "X1234567L", "A58818501", "Y1234567X");
        var validResult = await validator.ValidateAsync(valid);
        validResult.ShouldBeEmpty();

        var invalid = new SpanishDocumentHolder("invalid-dni", "invalid-nie", "invalid-cif", "invalid-nif");
        var invalidResult = await validator.ValidateAsync(invalid);
        invalidResult.Count.ShouldBe(4);
    }
    #endregion

    #region RuleSets and Groups Tests
    private sealed record ScopedRequest(string Name, string Email, string Password, int Id, string ShippingAddress);

    private sealed class ScopedRequestValidator : KyrolusAbstractValidator<ScopedRequest>
    {
        public ScopedRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();

            RuleSet("Create", () =>
            {
                RuleFor(x => x.Password).NotEmpty().WithGroups("Account", "Security");
            });

            RuleSet("Update", () =>
            {
                RuleFor(x => x.Id).Must(id => id > 0, "Id must be positive");
            });

            Group("Shipping", () =>
            {
                RuleFor(x => x.ShippingAddress).NotEmpty().WithGroups("Shipping", "Checkout");
            });

            RuleFor(x => x.Email).NotEmpty().InRuleSets("Create", "Update").WithGroup("Account");
        }
    }

    [Fact(DisplayName = "RuleSet executes only matching rules when specified in context")]
    public async Task RuleSet_Executes_Only_Matching_Rules()
    {
        var validator = new ScopedRequestValidator();
        var request = new ScopedRequest(Name: "", Email: "", Password: "", Id: 0, ShippingAddress: "");

        // When validating with "Create" RuleSet: Email (InRuleSets: Create), Password (RuleSet: Create) should fail. Id and Name (default) should NOT fail.
        var createContext = new KyrolusValidationContext(RuleSets: ["Create"]);
        var createResult = await validator.ValidateAsync(request, createContext);

        createResult.ShouldContain(f => f.PropertyName == "Password" && f.RuleSet == "Create");
        createResult.ShouldContain(f => f.PropertyName == "Email" && f.RuleSet == "Create");
        createResult.ShouldNotContain(f => f.PropertyName == "Name");
        createResult.ShouldNotContain(f => f.PropertyName == "Id");

        // When validating with "Create" + "default" RuleSets: Name (default), Email, Password should fail.
        var createWithDefaultContext = new KyrolusValidationContext(RuleSets: ["Create", "default"]);
        var createWithDefaultResult = await validator.ValidateAsync(request, createWithDefaultContext);

        createWithDefaultResult.ShouldContain(f => f.PropertyName == "Name");
        createWithDefaultResult.ShouldContain(f => f.PropertyName == "Password" && f.RuleSet == "Create");
        createWithDefaultResult.ShouldContain(f => f.PropertyName == "Email" && f.RuleSet == "Create");
        createWithDefaultResult.ShouldNotContain(f => f.PropertyName == "Id");

        // When validating with "Update" RuleSet: Email (InRuleSets: Update), Id (RuleSet: Update) should fail. Password should NOT fail.
        var updateContext = new KyrolusValidationContext(RuleSets: ["Update"]);
        var updateResult = await validator.ValidateAsync(request, updateContext);

        updateResult.ShouldContain(f => f.PropertyName == "Id" && f.RuleSet == "Update");
        updateResult.ShouldContain(f => f.PropertyName == "Email" && f.RuleSet == "Update");
        updateResult.ShouldNotContain(f => f.PropertyName == "Name");
        updateResult.ShouldNotContain(f => f.PropertyName == "Password");
    }

    [Fact(DisplayName = "WithGroups multi-group intersection filters correctly")]
    public async Task WithGroups_MultiGroup_Intersection_Filters_Correctly()
    {
        var validator = new ScopedRequestValidator();
        var request = new ScopedRequest(Name: "John", Email: "john@example.com", Password: "", Id: 1, ShippingAddress: "");

        // Context with Groups ["Security"]: Password has Groups ["Account", "Security"] -> intersects!
        var securityContext = new KyrolusValidationContext(Groups: ["Security"]);
        var securityResult = await validator.ValidateAsync(request, securityContext);

        securityResult.ShouldContain(f => f.PropertyName == "Password");
        securityResult.ShouldNotContain(f => f.PropertyName == "ShippingAddress");

        // Context with Groups ["Checkout"]: ShippingAddress has Groups ["Shipping", "Checkout"] -> intersects!
        var checkoutContext = new KyrolusValidationContext(Groups: ["Checkout"]);
        var checkoutResult = await validator.ValidateAsync(request, checkoutContext);

        checkoutResult.ShouldContain(f => f.PropertyName == "ShippingAddress");
        checkoutResult.ShouldNotContain(f => f.PropertyName == "Password");
    }
    #endregion
}
