namespace KyrolusSous.Validation.FluentValidation.UnitTests;

#region Test Models & Validators
public class TestSampleModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public List<string> Items { get; set; } = [];
}

public class TestSampleModelValidator : AbstractValidator<TestSampleModel>
{
    public TestSampleModelValidator()
    {
        RuleFor(x => x.Name).Required(x => x.Name).HasMaximumLength(10, x => x.Name);
        RuleFor(x => x.Color).IsColor(x => x.Color);
        RuleFor(x => x.Website).IsUrl(x => x.Website, isNullOrEmpty: true);
        RuleFor(x => x.NationalId).IsEgyptianNationalId(x => x.NationalId, isNullOrEmpty: true);
        RuleFor(x => x.CreatedBy).ShouldCreatedBySomeone(x => x.CreatedBy).IdCanNotBeZero(x => x.CreatedBy);
        RuleFor(x => x.Items).ArrayNotEmpty(x => x.Items);
    }
}

public class GroupTestModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class GroupTestModelValidator : AbstractValidator<GroupTestModel>
{
    public GroupTestModelValidator()
    {
        RuleSet("DefaultRuleSet", () =>
        {
            RuleFor(x => x.Title).NotEmpty().WithGroup("TitleGroup").WithSeverity(KyrolusValidationSeverity.Warning);
            RuleFor(x => x.Description).NotEmpty().WithGroup(new KyrolusValidationGroup("DescGroup")).WithSeverity(KyrolusValidationSeverity.Info);
            RuleFor(x => x.Category).NotEmpty().WithState(_ => new Dictionary<string, object?> { ["group"] = "MapGroup" });
        });
    }
}
#endregion

public class KyrolusValidationFluentValidationTests
{
    #region FluentValidationRequestValidator
    [Fact(DisplayName = "ValidateAsync returns empty when validator is not registered in DI")]
    public async Task ValidateAsync_ReturnsEmpty_WhenValidatorNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<TestSampleModel>(provider);
        var failures = await validator.ValidateAsync(new TestSampleModel());

        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync without context overload uses default context")]
    public async Task ValidateAsync_WithoutContext_UsesDefaultContext()
    {
        var fvValidator = new InlineValidator<GroupTestModel>();
        fvValidator.RuleFor(x => x.Title).NotEmpty();

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<GroupTestModel>>(fvValidator);
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<GroupTestModel>(provider);
        var failures = await validator.ValidateAsync(new GroupTestModel());

        failures.ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync returns empty when request is valid")]
    public async Task ValidateAsync_ReturnsEmpty_WhenRequestIsValid()
    {
        var services = new ServiceCollection();
        services.AddTransient<IValidator<TestSampleModel>, TestSampleModelValidator>();
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<TestSampleModel>(provider);
        var model = new TestSampleModel
        {
            Id = 1,
            Name = "Valid",
            Color = "#FFFFFF",
            Website = "https://example.com",
            NationalId = "29901011234567",
            CreatedBy = 10,
            Items = ["Item1"]
        };

        var failures = await validator.ValidateAsync(model);
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync maps errors, severities, groups, and metadata correctly")]
    public async Task ValidateAsync_MapsErrorsAndMetadataCorrectly()
    {
        var services = new ServiceCollection();
        services.AddTransient<IValidator<GroupTestModel>, GroupTestModelValidator>();
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<GroupTestModel>(provider);
        var model = new GroupTestModel();

        var context = new KyrolusValidationContext(RuleSets: ["DefaultRuleSet"]);
        var failures = await validator.ValidateAsync(model, context);

        failures.Count.ShouldBe(3);

        var titleFailure = failures.Single(f => f.PropertyName == "Title");
        titleFailure.Groups.ShouldNotBeNull();
        titleFailure.Groups.ShouldContain("TitleGroup");
        titleFailure.Severity.ShouldBe(KyrolusValidationSeverity.Warning);
        titleFailure.RuleSet.ShouldBe("DefaultRuleSet");

        var descFailure = failures.Single(f => f.PropertyName == "Description");
        descFailure.Groups.ShouldNotBeNull();
        descFailure.Groups.ShouldContain("DescGroup");
        descFailure.Severity.ShouldBe(KyrolusValidationSeverity.Info);

        var catFailure = failures.Single(f => f.PropertyName == "Category");
        catFailure.Groups.ShouldNotBeNull();
        catFailure.Groups.ShouldContain("MapGroup");
    }

    [Fact(DisplayName = "ValidateAsync with wildcard RuleSets includes all rule sets")]
    public async Task ValidateAsync_WithWildcardRuleSet_IncludesAllRuleSets()
    {
        var services = new ServiceCollection();
        services.AddTransient<IValidator<GroupTestModel>, GroupTestModelValidator>();
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<GroupTestModel>(provider);
        var context = new KyrolusValidationContext(RuleSets: ["*"]);
        var failures = await validator.ValidateAsync(new GroupTestModel(), context);

        failures.Count.ShouldBe(3);
    }

    [Fact(DisplayName = "ValidateAsync maps default Severity.Error to KyrolusValidationSeverity.Error")]
    public async Task ValidateAsync_MapsDefaultSeverityError()
    {
        var fvValidator = new InlineValidator<GroupTestModel>();
        fvValidator.RuleFor(x => x.Title).NotEmpty().WithSeverity(Severity.Error);

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<GroupTestModel>>(fvValidator);
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<GroupTestModel>(provider);
        var failures = await validator.ValidateAsync(new GroupTestModel());

        failures.Count.ShouldBe(1);
        failures[0].Severity.ShouldBe(KyrolusValidationSeverity.Error);
    }

    [Fact(DisplayName = "ValidateAsync resolves group when CustomState is a raw string")]
    public async Task ValidateAsync_ResolvesGroup_WhenCustomStateIsString()
    {
        var fvValidator = new InlineValidator<GroupTestModel>();
        fvValidator.RuleFor(x => x.Title).NotEmpty().WithState(_ => "StringGroup");

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<GroupTestModel>>(fvValidator);
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<GroupTestModel>(provider);
        var failures = await validator.ValidateAsync(new GroupTestModel());

        failures.Count.ShouldBe(1);
        failures[0].Groups.ShouldNotBeNull();
        failures[0].Groups!.ShouldContain("StringGroup");
        failures[0].Metadata.ShouldNotBeNull();
        failures[0].Metadata!["customState"].ShouldBe("StringGroup");
    }

    [Fact(DisplayName = "ResolveGroup handles unhandled CustomState gracefully")]
    public async Task ResolveGroup_HandlesUnhandledCustomStateGracefully()
    {
        var fvValidator = new InlineValidator<GroupTestModel>();
        fvValidator.RuleFor(x => x.Title).NotEmpty().WithState(_ => 12345);

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<GroupTestModel>>(fvValidator);
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<GroupTestModel>(provider);
        var failures = await validator.ValidateAsync(new GroupTestModel());

        failures.Count.ShouldBe(1);
        failures[0].Groups.ShouldBeNull();
    }
    #endregion

    #region KyrolusFluentValidationExtensions
    [Fact(DisplayName = "IsUrl validates http and https URLs correctly")]
    public void IsUrl_ValidatesUrlsCorrectly()
    {
        var validator = new InlineValidator<TestSampleModel>();
        validator.RuleFor(x => x.Website).IsUrl(x => x.Website, isNullOrEmpty: true);

        var validModel = new TestSampleModel { Website = "http://localhost:5000" };
        validator.Validate(validModel).IsValid.ShouldBeTrue();

        var emptyModel = new TestSampleModel { Website = "" };
        validator.Validate(emptyModel).IsValid.ShouldBeTrue();

        var invalidModel = new TestSampleModel { Website = "not-a-url" };
        var result = validator.Validate(invalidModel);
        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe(KyrolusValidationMessages.InvalidUrl);
    }

    [Fact(DisplayName = "IsEgyptianNationalId validates 14-digit Egyptian National IDs correctly")]
    public void IsEgyptianNationalId_ValidatesNationalIdCorrectly()
    {
        var validator = new InlineValidator<TestSampleModel>();
        validator.RuleFor(x => x.NationalId).IsEgyptianNationalId(x => x.NationalId, isNullOrEmpty: true);

        validator.Validate(new TestSampleModel { NationalId = "29505051234567" }).IsValid.ShouldBeTrue();
        validator.Validate(new TestSampleModel { NationalId = "30101011234567" }).IsValid.ShouldBeTrue();
        validator.Validate(new TestSampleModel { NationalId = "" }).IsValid.ShouldBeTrue();

        validator.Validate(new TestSampleModel { NationalId = "19505051234567" }).IsValid.ShouldBeFalse();
        validator.Validate(new TestSampleModel { NationalId = "2950505" }).IsValid.ShouldBeFalse();
        validator.Validate(new TestSampleModel { NationalId = "2950505123456A" }).IsValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "IsUrl and IsEgyptianNationalId use explicit propertyName when provided")]
    public void Extensions_UseExplicitPropertyName()
    {
        var validator = new InlineValidator<TestSampleModel>();
        validator.RuleFor(x => x.Website).IsUrl(x => x.Website, propertyName: "CustomUrlProp");
        validator.RuleFor(x => x.NationalId).IsEgyptianNationalId(x => x.NationalId, propertyName: "CustomNatIdProp");

        var model = new TestSampleModel { Website = "invalid", NationalId = "invalid" };
        var result = validator.Validate(model);

        result.Errors.Any(e => e.PropertyName == "CustomUrlProp").ShouldBeTrue();
        result.Errors.Any(e => e.PropertyName == "CustomNatIdProp").ShouldBeTrue();
    }

    [Fact(DisplayName = "IsEgyptianNationalId fails for invalid prefix starting digit")]
    public void IsEgyptianNationalId_FailsForInvalidPrefixDigit()
    {
        var validator = new InlineValidator<TestSampleModel>();
        validator.RuleFor(x => x.NationalId).IsEgyptianNationalId(x => x.NationalId);

        var model = new TestSampleModel { NationalId = "49505051234567" };
        var result = validator.Validate(model);

        result.IsValid.ShouldBeFalse();
    }

    [Fact(DisplayName = "WithSeverity maps KyrolusValidationSeverity.Error to Severity.Error")]
    public void WithSeverity_MapsErrorSeverity()
    {
        var validator = new InlineValidator<TestSampleModel>();
        validator.RuleFor(x => x.Name).NotEmpty().WithSeverity(KyrolusValidationSeverity.Error);

        var result = validator.Validate(new TestSampleModel());
        result.Errors[0].Severity.ShouldBe(Severity.Error);
    }

    [Fact(DisplayName = "IsSpanishDni, IsSpanishNie, IsSpanishCif, and IsSpanishNif validate correctly")]
    public void Spanish_ValidationExtensions_ValidateCorrectly()
    {
        var dniValidator = new InlineValidator<TestSampleModel>();
        dniValidator.RuleFor(x => x.NationalId).IsSpanishDni(x => x.NationalId, isNullOrEmpty: true);

        dniValidator.Validate(new TestSampleModel { NationalId = "12345678Z" }).IsValid.ShouldBeTrue();
        dniValidator.Validate(new TestSampleModel { NationalId = "" }).IsValid.ShouldBeTrue();
        dniValidator.Validate(new TestSampleModel { NationalId = "12345678A" }).IsValid.ShouldBeFalse();

        var nieValidator = new InlineValidator<TestSampleModel>();
        nieValidator.RuleFor(x => x.NationalId).IsSpanishNie(x => x.NationalId, isNullOrEmpty: true);

        nieValidator.Validate(new TestSampleModel { NationalId = "X1234567L" }).IsValid.ShouldBeTrue();
        nieValidator.Validate(new TestSampleModel { NationalId = "" }).IsValid.ShouldBeTrue();
        nieValidator.Validate(new TestSampleModel { NationalId = "A1234567L" }).IsValid.ShouldBeFalse();

        var cifValidator = new InlineValidator<TestSampleModel>();
        cifValidator.RuleFor(x => x.NationalId).IsSpanishCif(x => x.NationalId, isNullOrEmpty: true);

        cifValidator.Validate(new TestSampleModel { NationalId = "A58818501" }).IsValid.ShouldBeTrue();
        cifValidator.Validate(new TestSampleModel { NationalId = "" }).IsValid.ShouldBeTrue();
        cifValidator.Validate(new TestSampleModel { NationalId = "Z58818501" }).IsValid.ShouldBeFalse();

        var nifValidator = new InlineValidator<TestSampleModel>();
        nifValidator.RuleFor(x => x.NationalId).IsSpanishNif(x => x.NationalId, propertyName: "SpanishFiscalId");

        nifValidator.Validate(new TestSampleModel { NationalId = "12345678Z" }).IsValid.ShouldBeTrue();
        nifValidator.Validate(new TestSampleModel { NationalId = "X1234567L" }).IsValid.ShouldBeTrue();
        nifValidator.Validate(new TestSampleModel { NationalId = "A58818501" }).IsValid.ShouldBeTrue();
        
        var invalidResult = nifValidator.Validate(new TestSampleModel { NationalId = "invalid-nif" });
        invalidResult.IsValid.ShouldBeFalse();
        invalidResult.Errors.Any(e => e.PropertyName == "SpanishFiscalId").ShouldBeTrue();
    }
    #endregion

    #region ServiceCollectionExtensions
    [Fact(DisplayName = "AddKyrolusFluentValidation registers validators in DI")]
    public void AddKyrolusFluentValidation_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusFluentValidation();
        var provider = services.BuildServiceProvider();

        var validator = provider.GetService<IKyrolusRequestValidator<TestSampleModel>>();
        validator.ShouldNotBeNull();
        validator.ShouldBeOfType<FluentValidationRequestValidator<TestSampleModel>>();

        var contextValidator = provider.GetService<IKyrolusRequestValidatorWithContext<TestSampleModel>>();
        contextValidator.ShouldNotBeNull();
    }
    #endregion

    #region KyrolusValidationMessages
    [Fact(DisplayName = "KyrolusValidationMessages returns formatted message strings")]
    public void KyrolusValidationMessages_ReturnsFormattedMessages()
    {
        var model = new TestSampleModel();
        KyrolusValidationMessages.EntityNotFound(model).ShouldBe("TestSampleModel not found");
        KyrolusValidationMessages.EntityAlreadyExists(model).ShouldBe("TestSampleModel already exists");
        KyrolusValidationMessages.ForeignKeyViolation("User", "5").ShouldBe("User with id 5 not found");
        KyrolusValidationMessages.ShouldBeGreaterThanZero("Age").ShouldBe("Age should be greater than zero.");
        KyrolusValidationMessages.ExceedsMaxLength(20).ShouldBe("can not have more than 20 characters.");
        KyrolusValidationMessages.DuplicateEntityWithProperty("User", "Email").ShouldContain("Email");
    }
    #endregion

    #region Multi-Group Tests
    private sealed class MultiGroupModel
    {
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    private sealed class MultiGroupModelValidator : AbstractValidator<MultiGroupModel>
    {
        public MultiGroupModelValidator()
        {
            RuleFor(x => x.Email).NotEmpty().WithGroups("Account", "Contact");
            RuleFor(x => x.Phone).NotEmpty().WithGroups(["Support", "Contact"]);
        }
    }

    [Fact(DisplayName = "WithGroups registers multiple groups on FluentValidation failure")]
    public async Task WithGroups_Registers_Multiple_Groups_Correctly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<MultiGroupModel>, MultiGroupModelValidator>();
        var provider = services.BuildServiceProvider();

        var validator = new FluentValidationRequestValidator<MultiGroupModel>(provider);
        var failures = await validator.ValidateAsync(new MultiGroupModel());

        failures.Count.ShouldBe(2);

        var emailFailure = failures.First(f => f.PropertyName == "Email");
        emailFailure.Groups.ShouldNotBeNull();
        emailFailure.Groups.ShouldContain("Account");
        emailFailure.Groups.ShouldContain("Contact");

        var phoneFailure = failures.First(f => f.PropertyName == "Phone");
        phoneFailure.Groups.ShouldNotBeNull();
        phoneFailure.Groups.ShouldContain("Support");
        phoneFailure.Groups.ShouldContain("Contact");
    }
    #endregion
}
