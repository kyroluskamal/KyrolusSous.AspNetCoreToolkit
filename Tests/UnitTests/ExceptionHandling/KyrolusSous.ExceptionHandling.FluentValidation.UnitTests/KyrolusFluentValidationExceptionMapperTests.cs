using System.Net;
using FluentValidation;
using FluentValidation.Results;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.ExceptionHandling.FluentValidation.UnitTests;

public class KyrolusFluentValidationExceptionMapperTests
{
    private readonly KyrolusFluentValidationExceptionMapper mapper = new();
    private readonly KyrolusErrorContext context = new(
        TraceId: "trace-fv-123",
        CorrelationId: "corr-fv-456",
        UserId: "user-fv",
        TenantId: "tenant-fv",
        Path: "/api/users",
        Method: "POST",
        Culture: null);

    [Fact(DisplayName = "Order should return -50")]
    public void Order_Should_Be_Minus50()
    {
        mapper.Order.ShouldBe(-50);
    }

    [Fact(DisplayName = "TryMap with multi-error ValidationException should generate smart dynamic detail")]
    public void TryMap_MultiError_ValidationException_Should_Generate_Smart_Detail()
    {
        var failures = new List<ValidationFailure>
        {
            new("Email", "Invalid email format") { ErrorCode = "invalid_email" },
            new("Age", "Must be at least 18") { ErrorCode = null }
        };
        var ex = new ValidationException(failures);
        ex.Data["CustomFvData"] = "ValidationAttempt1";

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        mapping.ShouldLog.ShouldBeFalse();
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.Validation);
        mapping.Error.Title.ShouldBe("Validation failed");
        mapping.Error.Detail.ShouldBe("2 validation errors occurred in fields: Email, Age.");
        mapping.Error.TraceId.ShouldBe("trace-fv-123");
        mapping.Error.Errors.ShouldNotBeNull();
        mapping.Error.Errors.Count.ShouldBe(2);

        var firstError = mapping.Error.Errors[0];
        firstError.Field.ShouldBe("Email");
        firstError.Code.ShouldBe("invalid_email");
        firstError.Message.ShouldBe("Invalid email format");

        var secondError = mapping.Error.Errors[1];
        secondError.Field.ShouldBe("Age");
        secondError.Code.ShouldBe("validation_error");
        secondError.Message.ShouldBe("Must be at least 18");

        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata["CustomFvData"]!.ToString().ShouldBe("ValidationAttempt1");
    }

    [Fact(DisplayName = "TryMap with single error ValidationException should generate single field detail")]
    public void TryMap_SingleError_ValidationException_Should_Generate_Single_Field_Detail()
    {
        var failures = new List<ValidationFailure>
        {
            new("Username", "Username is required")
        };
        var ex = new ValidationException(failures);

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.Error.Detail.ShouldBe("Validation failed on 'Username': Username is required");
    }

    [Fact(DisplayName = "TryMap with custom KyrolusFluentValidationOptions should apply custom title and formatter")]
    public void TryMap_WithCustomOptions_Should_Apply_Custom_Title_And_Formatter()
    {
        var customOptions = Options.Create(new KyrolusFluentValidationOptions
        {
            DefaultTitle = "Input Verification Error",
            DetailFormatter = (ex, errors) => $"Found {errors.Count} problem(s) in payload."
        });

        var customMapper = new KyrolusFluentValidationExceptionMapper(customOptions);

        var failures = new List<ValidationFailure>
        {
            new("Email", "Invalid")
        };
        var ex = new ValidationException(failures);

        var mapped = customMapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeTrue();
        mapping.Error.Title.ShouldBe("Input Verification Error");
        mapping.Error.Detail.ShouldBe("Found 1 problem(s) in payload.");
    }

    [Fact(DisplayName = "TryMap with unrelated exception should return false and null mapping")]
    public void TryMap_UnrelatedException_Should_Return_False()
    {
        var ex = new InvalidOperationException("Some operation error");

        var mapped = mapper.TryMap(ex, context, out var mapping);

        mapped.ShouldBeFalse();
        mapping.ShouldBeNull();
    }

    [Fact(DisplayName = "AddKyrolusFluentValidationExceptionHandling should register mapper and options in DI")]
    public void AddKyrolusFluentValidationExceptionHandling_Should_Register_Mapper_And_Options()
    {
        var services = new ServiceCollection();
        services.AddKyrolusFluentValidationExceptionHandling(opt =>
        {
            opt.DefaultTitle = "Custom Validation Title";
        });

        var provider = services.BuildServiceProvider();
        var mappers = provider.GetServices<IKyrolusExceptionMapper>().ToList();
        var options = provider.GetRequiredService<IOptions<KyrolusFluentValidationOptions>>().Value;

        mappers.ShouldNotBeEmpty();
        mappers.ShouldContain(m => m is KyrolusFluentValidationExceptionMapper);
        options.DefaultTitle.ShouldBe("Custom Validation Title");
    }
}
