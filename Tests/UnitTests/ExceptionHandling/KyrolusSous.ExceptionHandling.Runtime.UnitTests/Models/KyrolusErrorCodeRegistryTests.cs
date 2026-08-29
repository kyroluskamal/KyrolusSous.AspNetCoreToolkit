using System.Text.RegularExpressions;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Models;

public class KyrolusErrorCodeRegistryTests : IDisposable
{
    public KyrolusErrorCodeRegistryTests()
    {
        KyrolusErrorCodeRegistry.ResetToDefault();
    }

    public void Dispose()
    {
        KyrolusErrorCodeRegistry.ResetToDefault();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "Registry should contain core default definitions on startup")]
    public void Registry_Should_Contain_Core_Defaults()
    {
        KyrolusErrorCodeRegistry.TryGet(KyrolusErrorCodes.Validation, out var validationDef).ShouldBeTrue();
        validationDef.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        validationDef.Title.ShouldBe("Validation failed");
        validationDef.ShouldLog.ShouldBeFalse();

        KyrolusErrorCodeRegistry.TryGet(KyrolusErrorCodes.NotFound, out var notFoundDef).ShouldBeTrue();
        notFoundDef.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        KyrolusErrorCodeRegistry.TryGet(KyrolusErrorCodes.InternalError, out var internalDef).ShouldBeTrue();
        internalDef.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        internalDef.ShouldLog.ShouldBeTrue();

        var snapshot = KyrolusErrorCodeRegistry.Snapshot();
        snapshot.Count.ShouldBeGreaterThanOrEqualTo(14);
    }

    [Fact(DisplayName = "Register should add new valid code definition")]
    public void Register_Should_Add_Valid_Code()
    {
        var code = "custom_order_error_" + Guid.NewGuid().ToString("N")[..8];
        var def = new KyrolusErrorCodeDefinition(code, "Custom Order Error", HttpStatusCode.PaymentRequired);

        KyrolusErrorCodeRegistry.Register(def);

        KyrolusErrorCodeRegistry.TryGet(code, out var retrieved).ShouldBeTrue();
        retrieved.ShouldBe(def);
    }

    [Fact(DisplayName = "Register should throw ArgumentNullException when definition is null")]
    public void Register_Should_Throw_On_Null_Definition()
    {
        Should.Throw<ArgumentNullException>(() => KyrolusErrorCodeRegistry.Register(null!));
    }

    [Fact(DisplayName = "Register should throw KyrolusErrorCodeRegistryException when code is empty or whitespace")]
    public void Register_Should_Throw_On_Empty_Code()
    {
        var def = new KyrolusErrorCodeDefinition("", "Empty", HttpStatusCode.BadRequest);
        Should.Throw<KyrolusErrorCodeRegistryException>(() => KyrolusErrorCodeRegistry.Register(def));
    }

    [Fact(DisplayName = "Register should throw KyrolusErrorCodeRegistryException when code does not match naming convention")]
    public void Register_Should_Throw_On_Invalid_Convention()
    {
        var def = new KyrolusErrorCodeDefinition("Invalid-Kebab-Code", "Invalid", HttpStatusCode.BadRequest);
        Should.Throw<KyrolusErrorCodeRegistryException>(() => KyrolusErrorCodeRegistry.Register(def));
    }

    [Fact(DisplayName = "Register should throw KyrolusErrorCodeRegistryException when code is duplicate")]
    public void Register_Should_Throw_On_Duplicate_Code()
    {
        var def = new KyrolusErrorCodeDefinition(KyrolusErrorCodes.NotFound, "Duplicate", HttpStatusCode.NotFound);
        Should.Throw<KyrolusErrorCodeRegistryException>(() => KyrolusErrorCodeRegistry.Register(def));
    }

    [Fact(DisplayName = "SetCodePattern with string should configure custom regex pattern")]
    public void SetCodePattern_String_Should_Configure_Pattern()
    {
        KyrolusErrorCodeRegistry.SetCodePattern("^[A-Z]{3}_[0-9]{3}$");

        KyrolusErrorCodeRegistry.IsConfigured.ShouldBeTrue();
        KyrolusErrorCodeRegistry.ConfiguredMethod.ShouldBe(nameof(KyrolusErrorCodeRegistry.SetCodePattern));

        KyrolusErrorCodeRegistry.IsValidCode("ERR_123").ShouldBeTrue();
        KyrolusErrorCodeRegistry.IsValidCode("invalid_code").ShouldBeFalse();
    }

    [Fact(DisplayName = "SetCodePattern with Regex object should configure regex")]
    public void SetCodePattern_Regex_Should_Configure()
    {
        var regex = new Regex("^[a-z]+-[a-z]+$");
        KyrolusErrorCodeRegistry.SetCodePattern(regex);

        KyrolusErrorCodeRegistry.IsValidCode("hello-world").ShouldBeTrue();
        KyrolusErrorCodeRegistry.IsValidCode("hello_world").ShouldBeFalse();
    }

    [Fact(DisplayName = "SetCodePattern should throw on null or whitespace pattern")]
    public void SetCodePattern_Should_Throw_On_Invalid_Input()
    {
        Should.Throw<ArgumentException>(() => KyrolusErrorCodeRegistry.SetCodePattern(""));
        Should.Throw<ArgumentException>(() => KyrolusErrorCodeRegistry.SetCodePattern("   "));
        Should.Throw<ArgumentNullException>(() => KyrolusErrorCodeRegistry.SetCodePattern((Regex)null!));
    }

    [Fact(DisplayName = "SetCustomValidator should configure custom validation function")]
    public void SetCustomValidator_Should_Work()
    {
        KyrolusErrorCodeRegistry.SetCustomValidator(code => code.StartsWith("valid:"));

        KyrolusErrorCodeRegistry.IsValidCode("valid:item").ShouldBeTrue();
        KyrolusErrorCodeRegistry.IsValidCode("invalid:item").ShouldBeFalse();
    }

    [Fact(DisplayName = "SetCustomValidator should throw on null validator")]
    public void SetCustomValidator_Should_Throw_On_Null()
    {
        Should.Throw<ArgumentNullException>(() => KyrolusErrorCodeRegistry.SetCustomValidator(null!));
    }

    [Fact(DisplayName = "DisableValidation should allow any code pattern")]
    public void DisableValidation_Should_Allow_Any_Pattern()
    {
        KyrolusErrorCodeRegistry.DisableValidation();

        KyrolusErrorCodeRegistry.IsValidCode("ANYTHING goes here 123!@#").ShouldBeTrue();
    }

    [Fact(DisplayName = "Configuring registry multiple times should throw KyrolusErrorCodeRegistryException")]
    public void Multiple_Configurations_Should_Throw()
    {
        KyrolusErrorCodeRegistry.DisableValidation();

        Should.Throw<KyrolusErrorCodeRegistryException>(() => KyrolusErrorCodeRegistry.SetCustomValidator(c => true));
    }

    [Fact(DisplayName = "IsValidCode with null or empty should return false")]
    public void IsValidCode_Should_Return_False_For_Null_Or_Empty()
    {
        KyrolusErrorCodeRegistry.IsValidCode(null!).ShouldBeFalse();
        KyrolusErrorCodeRegistry.IsValidCode("").ShouldBeFalse();
        KyrolusErrorCodeRegistry.IsValidCode("   ").ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusExceptionMapping helper methods should work correctly")]
    public void KyrolusExceptionMapping_Helpers_Should_Work()
    {
        var mapping = KyrolusExceptionMapping.Create(
            code: "test_code",
            title: "Test Title",
            statusCode: HttpStatusCode.BadGateway)
            .AsTransient(true)
            .WithoutLogging();

        mapping.IsTransient.ShouldBeTrue();
        mapping.ShouldLog.ShouldBeFalse();

        var withLogging = mapping.WithLogging(true);
        withLogging.ShouldLog.ShouldBeTrue();
    }

    [Fact(DisplayName = "StrictMode when disabled should return false for unregistered codes without throwing")]
    public void StrictMode_Disabled_Should_Return_False_For_Unregistered_Code()
    {
        KyrolusErrorCodeRegistry.StrictMode = false;

        var result = KyrolusErrorCodeRegistry.TryGet("unregistered_test_code", out var def);

        result.ShouldBeFalse();
        def.ShouldBeNull();
    }

    [Fact(DisplayName = "StrictMode when enabled should throw KyrolusErrorCodeRegistryException for unregistered codes")]
    public void StrictMode_Enabled_Should_Throw_For_Unregistered_Code()
    {
        KyrolusErrorCodeRegistry.EnableStrictMode();

        var ex = Should.Throw<KyrolusErrorCodeRegistryException>(() =>
            KyrolusErrorCodeRegistry.TryGet("unregistered_strict_code", out _));

        ex.Message.ShouldContain("Strict Mode Violation");
        ex.Message.ShouldContain("unregistered_strict_code");
    }

    [Fact(DisplayName = "StrictMode when enabled should still succeed for registered codes")]
    public void StrictMode_Enabled_Should_Succeed_For_Registered_Code()
    {
        KyrolusErrorCodeRegistry.EnableStrictMode();

        var result = KyrolusErrorCodeRegistry.TryGet(KyrolusErrorCodes.NotFound, out var def);

        result.ShouldBeTrue();
        def.ShouldNotBeNull();
        def.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "ResetToDefault should reset StrictMode to false")]
    public void ResetToDefault_Should_Reset_StrictMode()
    {
        KyrolusErrorCodeRegistry.EnableStrictMode();
        KyrolusErrorCodeRegistry.StrictMode.ShouldBeTrue();

        KyrolusErrorCodeRegistry.ResetToDefault();

        KyrolusErrorCodeRegistry.StrictMode.ShouldBeFalse();
    }
}
