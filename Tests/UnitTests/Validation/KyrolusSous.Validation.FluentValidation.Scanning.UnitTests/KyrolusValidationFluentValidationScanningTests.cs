namespace KyrolusSous.Validation.FluentValidation.Scanning.UnitTests;

#region Test Request & Validator
public class ScanningTestRequest
{
    public string Name { get; set; } = string.Empty;
}

public class ScanningTestRequestValidator : AbstractValidator<ScanningTestRequest>
{
    public ScanningTestRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
#endregion

public class KyrolusValidationFluentValidationScanningTests
{
    [Fact(DisplayName = "AddKyrolusFluentValidationScanning scans and registers validators in DI")]
    public void AddKyrolusFluentValidationScanning_RegistersValidatorsFromAssemblies()
    {
        var services = new ServiceCollection();
        services.AddKyrolusFluentValidationScanning(typeof(ScanningTestRequestValidator).Assembly);
        var provider = services.BuildServiceProvider();

        var validator = provider.GetService<IValidator<ScanningTestRequest>>();
        validator.ShouldNotBeNull();
        validator.ShouldBeOfType<ScanningTestRequestValidator>();

        var requestValidator = provider.GetService<IKyrolusRequestValidator<ScanningTestRequest>>();
        requestValidator.ShouldNotBeNull();
    }

    [Fact(DisplayName = "AddKyrolusFluentValidationScanningFromAssemblyContaining scans and registers validators in DI")]
    public void AddKyrolusFluentValidationScanningFromAssemblyContaining_RegistersValidators()
    {
        var services = new ServiceCollection();
        services.AddKyrolusFluentValidationScanningFromAssemblyContaining<ScanningTestRequestValidator>();
        var provider = services.BuildServiceProvider();

        var validator = provider.GetService<IValidator<ScanningTestRequest>>();
        validator.ShouldNotBeNull();
        validator.ShouldBeOfType<ScanningTestRequestValidator>();

        var requestValidator = provider.GetService<IKyrolusRequestValidatorWithContext<ScanningTestRequest>>();
        requestValidator.ShouldNotBeNull();
    }
}
