namespace KyrolusSous.Validation.FluentValidation;

/// <summary>
/// Standardized message templates used as the default <c>.WithMessage(...)</c> text by
/// <see cref="KyrolusFluentValidationExtensions"/>'s rule extensions, kept centralized here so wording stays
/// consistent across every rule instead of being duplicated as inline string literals at each call site.
/// </summary>
public static class KyrolusValidationMessages
{
    /// <summary>Message for "this entity does not exist", parameterized by the entity instance (its runtime type name is used).</summary>
    public static readonly Func<object, string> EntityNotFound = entity => $"{entity.GetType().Name} not found";

    /// <summary>Message for "this entity already exists", parameterized by the entity instance (its runtime type name is used).</summary>
    public static readonly Func<object, string> EntityAlreadyExists = entity => $"{entity.GetType().Name} already exists";

    /// <summary>Message for a dangling foreign key reference, parameterized by the referenced entity's name and the missing id's value.</summary>
    public static readonly Func<string, string, string> ForeignKeyViolation = (entityName, propertyValue) => $"{entityName} with id {propertyValue} not found";

    /// <summary>Message for "this numeric property must be positive", parameterized by the property's display name.</summary>
    public static readonly Func<string, string> ShouldBeGreaterThanZero = propertyName => $"{propertyName} should be greater than zero.";

    /// <summary>Generic fallback message for an unspecified validation error.</summary>
    public const string ValidationErrorMessage = "Validation error occurred";

    /// <summary>Message fragment for <c>ArrayNotEmpty</c>/<c>Required</c>-style "must not be empty" failures.</summary>
    public const string CanNotBeEmpty = "can not be empty.";

    /// <summary>Message fragment for <c>IdCanNotBeZero</c>-style "must be a positive identifier" failures.</summary>
    public const string CanNotBeZero = "can not be zero.";

    /// <summary>Message fragment for <c>ShouldCreatedBySomeone</c>-style "audit field must be populated" failures.</summary>
    public const string ShouldBeCreatedBySomeone = "should be created by someone.";

    /// <summary>Message fragment for <c>Required</c>-style "field is required" failures.</summary>
    public const string IsRequired = "is required.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsUrl{T}"/> failures.</summary>
    public const string InvalidUrl = "is not a valid URL.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsColor{T}"/> failures.</summary>
    public const string InvalidHexColor = "Color must be a valid hexadecimal code in the form '#RRGGBB'.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsEgyptianNationalId{T}"/> failures.</summary>
    public const string InvalidEgyptianNationalId = "is not a valid Egyptian National ID.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsSpanishDni{T}"/> failures.</summary>
    public const string InvalidSpanishDni = "is not a valid Spanish DNI.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsSpanishNie{T}"/> failures.</summary>
    public const string InvalidSpanishNie = "is not a valid Spanish NIE.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsSpanishCif{T}"/> failures.</summary>
    public const string InvalidSpanishCif = "is not a valid Spanish CIF.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.IsSpanishNif{T}"/> failures.</summary>
    public const string InvalidSpanishNif = "is not a valid Spanish NIF.";

    /// <summary>Message for <see cref="KyrolusFluentValidationExtensions.HasMaximumLength{T}"/> failures, parameterized by the configured maximum length.</summary>
    public static readonly Func<int, string> ExceedsMaxLength = length => $"can not have more than {length} characters.";

    /// <summary>Message for a uniqueness-constraint failure, parameterized by the entity's name and the duplicated property's name.</summary>
    public static readonly Func<string, string, string> DuplicateEntityWithProperty =
        (entityName, propertyName) => $"There is a {entityName} with the same <<< {propertyName} >>> in the database. You can not duplicate it.";
}
