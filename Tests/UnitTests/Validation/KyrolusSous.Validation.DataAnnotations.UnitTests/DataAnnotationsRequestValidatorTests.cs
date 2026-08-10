namespace KyrolusSous.Validation.DataAnnotations.Unitests;

public class DataAnnotationsRequestValidatorTests
{
    [Fact(DisplayName = "ValidateAsync should return ValidationFailures when request is null")]
    public async Task ValidateAsync_ReturnsValidationFailures_WhenRequestIsNull()
    {
        var validator = new DataAnnotationsRequestValidator<TestUserRequest>();
        IReadOnlyList<KyrolusValidationFailure> result = await validator.ValidateAsync(null!);
        result.ShouldNotBeNull();
        result[0].PropertyName.ShouldBe(string.Empty);
        result[0].ErrorMessage.ShouldBe("Request is required.");
    }
}
