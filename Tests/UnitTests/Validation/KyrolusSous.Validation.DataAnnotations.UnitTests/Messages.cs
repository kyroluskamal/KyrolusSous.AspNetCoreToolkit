namespace KyrolusSous.Validation.DataAnnotations.UnitTests;

public sealed record TestUserRequest(
    [property: Required(ErrorMessage = "Name is required.")]
    [property: StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters.")]
    string Name,

    [property: Range(18, 120, ErrorMessage = "Age must be between 18 and 120.")]
    int Age,

    [property: EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    string? Address = null
);
