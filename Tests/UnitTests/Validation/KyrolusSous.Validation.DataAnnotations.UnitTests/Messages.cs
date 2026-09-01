namespace KyrolusSous.Validation.DataAnnotations.UnitTests;

public sealed record TestUserRequest(
    [property: Required(ErrorMessage = "Name is required.")]
    [property: StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters.")]
    string Name,

    [property: Range(18, 120, ErrorMessage = "Age must be between 18 and 120.")]
    int Age,

    [property: EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,
    [property: StringLength(100)]
    string? Address = null
);

public sealed class ValidatableRequestWithNoMembers : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield return new ValidationResult("Object level validation failed.");
        yield return new ValidationResult(errorMessage: null);
    }
}

public sealed class ScopedTestRequest
{
    [Required(ErrorMessage = "Password is required.")]
    [KyrolusValidationScope(RuleSets = ["Create"])]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "CreatedBy is required.")]
    [KyrolusValidationScope(Groups = ["Audit"])]
    public string CreatedBy { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;
}

public sealed class ContextCapturingRequest : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (validationContext.Items.TryGetValue(nameof(KyrolusValidationContext), out var ctxObj) &&
            ctxObj is KyrolusValidationContext capturedContext)
        {
            // Successfully retrieved our KyrolusValidationContext!
            yield return ValidationResult.Success!;
        }
        else
        {
            yield return new ValidationResult("KyrolusValidationContext was not passed in Items dictionary.");
        }
    }
}
