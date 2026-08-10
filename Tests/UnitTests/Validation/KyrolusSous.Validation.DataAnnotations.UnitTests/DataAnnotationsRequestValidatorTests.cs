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
    }
}
