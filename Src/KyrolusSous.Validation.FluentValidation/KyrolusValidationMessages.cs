namespace KyrolusSous.Validation.FluentValidation;

public static class KyrolusValidationMessages
{
    public static readonly Func<object, string> EntityNotFound = entity => $"{entity.GetType().Name} not found";
    public static readonly Func<object, string> EntityAlreadyExists = entity => $"{entity.GetType().Name} already exists";
    public static readonly Func<string, string, string> ForeignKeyViolation = (entityName, propertyValue) => $"{entityName} with id {propertyValue} not found";
    public static readonly Func<string, string> ShouldBeGreaterThanZero = propertyName => $"{propertyName} should be greater than zero.";

    public const string ValidationErrorMessage = "Validation error occurred";
    public const string CanNotBeEmpty = "can not be empty.";
    public const string CanNotBeZero = "can not be zero.";
    public const string ShouldBeCreatedBySomeone = "should be created by someone.";
    public const string IsRequired = "is required.";
    public const string InvalidUrl = "is not a valid URL.";
    public const string InvalidHexColor = "Color must be a valid hexadecimal code in the form '#RRGGBB'.";
    public static readonly Func<int, string> ExceedsMaxLength = length => $"can have more than {length} characters.";
    public static readonly Func<string, string, string> DuplicateEntityWithProperty =
        (entityName, propertyName) => $"There is a {entityName} with the same <<< {propertyName} >>> in the database. You can not duplicate it.";
}
