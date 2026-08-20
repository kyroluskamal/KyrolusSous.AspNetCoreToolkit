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
        titleFailure.Group.ShouldBe("TitleGroup");
        titleFailure.Severity.ShouldBe(KyrolusValidationSeverity.Warning);
        titleFailure.RuleSet.ShouldBe("DefaultRuleSet");

        var descFailure = failures.Single(f => f.PropertyName == "Description");
        descFailure.Group.ShouldBe("DescGroup");
        descFailure.Severity.ShouldBe(KyrolusValidationSeverity.Info);

        var catFailure = failures.Single(f => f.PropertyName == "Category");
        catFailure.Group.ShouldBe("MapGroup");
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
        failures[0].Group.ShouldBe("StringGroup");
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
        failures[0].Group.ShouldBeNull();
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

    [Fact(DisplayName = "ReturnMemberExpression returns empty string when expression is non-member")]
    public void Extensions_HandleNonMemberExpressions()
    {
        var validator = new InlineValidator<TestSampleModel>();
        validator.RuleFor(x => x.Name).Required(x => "ConstantString", propertyName: "FallbackName");

        var result = validator.Validate(new TestSampleModel());
        result.Errors.ShouldNotBeEmpty();
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

    [Fact(DisplayName = "AddKyrolusFluentValidationFromAssemblyContaining scans and registers validators")]
    public void AddKyrolusFluentValidationFromAssemblyContaining_ScansValidators()
    {
        var services = new ServiceCollection();
        services.AddKyrolusFluentValidationFromAssemblyContaining<TestSampleModelValidator>();
        var provider = services.BuildServiceProvider();

        var fvValidator = provider.GetService<IValidator<TestSampleModel>>();
        fvValidator.ShouldNotBeNull();
        fvValidator.ShouldBeOfType<TestSampleModelValidator>();
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
}
