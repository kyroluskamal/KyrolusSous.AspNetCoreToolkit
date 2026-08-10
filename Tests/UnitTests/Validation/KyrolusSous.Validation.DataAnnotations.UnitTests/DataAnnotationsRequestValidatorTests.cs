namespace KyrolusSous.Validation.DataAnnotations.UnitTests;

public class DataAnnotationsRequestValidatorTests
{
    [Fact(DisplayName = "ValidateAsync should return ValidationFailures when request is null")]
    public async Task ValidateAsync_ReturnsValidationFailures_WhenRequestIsNull()
    {
        IReadOnlyList<KyrolusValidationFailure> result = await ValidationDataAnnotationTestsHelper.ValidateAsync(null!, KyrolusValidationContext.Default);
        result.ShouldNotBeNull();
        result[0].PropertyName.ShouldBe(string.Empty);
        result[0].ErrorMessage.ShouldBe("Request is required.");
    }

    [Fact(DisplayName = "ValidateAsync should return ValidationFailures when request is invalid")]
    public async Task ValidateAsync_ReturnsValidationFailures_WhenRequestIsInvalid()
    {
        var request = new TestUserRequest(null!, 17, "invalid-email");

        IReadOnlyList<KyrolusValidationFailure> result = await ValidationDataAnnotationTestsHelper.ValidateAsync(request, KyrolusValidationContext.Default);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);

        var nameFailure = result.FirstOrDefault(f => f.PropertyName == nameof(TestUserRequest.Name));
        nameFailure.ShouldNotBeNull();
        nameFailure!.ErrorMessage.ShouldBe("Name is required.");

        var ageFailure = result.FirstOrDefault(f => f.PropertyName == nameof(TestUserRequest.Age));
        ageFailure.ShouldNotBeNull();
        ageFailure!.ErrorMessage.ShouldBe("Age must be between 18 and 120.");

        var emailFailure = result.FirstOrDefault(f => f.PropertyName == nameof(TestUserRequest.Email));
        emailFailure.ShouldNotBeNull();
        emailFailure!.ErrorMessage.ShouldBe("Invalid email format.");

        var addressFailure = result.FirstOrDefault(f => f.PropertyName == nameof(TestUserRequest.Address));
        addressFailure.ShouldBeNull();
        addressFailure?.ErrorMessage.ShouldBe("Validation error.");
    }

    [Fact(DisplayName = "ValidateAsync should return empty list when request is valid")]
    public async Task ValidateAsync_ReturnsEmptyList_WhenRequestIsValid()
    {
        var request = new TestUserRequest("John Doe", 25, "john.doe@example.com", "123 Main St");

        IReadOnlyList<KyrolusValidationFailure> result = await ValidationDataAnnotationTestsHelper.ValidateAsync(request, KyrolusValidationContext.Default);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "ValidateAsync handles object-level validation failures without member names and fallback error messages")]
    public async Task ValidateAsync_HandlesObjectLevelAndNullErrorMessageFailures()
    {
        var validator = new DataAnnotationsRequestValidator<ValidatableRequestWithNoMembers>();
        var request = new ValidatableRequestWithNoMembers();

        IReadOnlyList<KyrolusValidationFailure> result = await validator.ValidateAsync(request, KyrolusValidationContext.Default);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result[0].PropertyName.ShouldBe(string.Empty);
        result[0].ErrorMessage.ShouldBe("Object level validation failed.");

        result[1].PropertyName.ShouldBe(string.Empty);
        result[1].ErrorMessage.ShouldBe("Validation error.");
    }

    [Fact(DisplayName = "ValidateAsync passes KyrolusValidationContext into ValidationContext.Items dictionary")]
    public async Task ValidateAsync_PassesKyrolusValidationContext_ToValidationItems()
    {
        var validator = new DataAnnotationsRequestValidator<ContextCapturingRequest>();
        var request = new ContextCapturingRequest();
        var customContext = KyrolusValidationContext.Default;

        IReadOnlyList<KyrolusValidationFailure> result = await validator.ValidateAsync(request, customContext);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "ValidateAsync with cancelled CancellationToken throws OperationCanceledException")]
    public async Task ValidateAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        var validator = new DataAnnotationsRequestValidator<TestUserRequest>();
        var request = new TestUserRequest("John Doe", 25, "john.doe@example.com");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await validator.ValidateAsync(request, KyrolusValidationContext.Default, cts.Token);
        });
    }

    [Fact(DisplayName = "ValidateAsync with null KyrolusValidationContext uses default context")]
    public async Task ValidateAsync_WithNullContext_UsesDefaultContext()
    {
        var validator = new DataAnnotationsRequestValidator<ContextCapturingRequest>();
        var request = new ContextCapturingRequest();

        IReadOnlyList<KyrolusValidationFailure> result = await validator.ValidateAsync(request);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }
}
