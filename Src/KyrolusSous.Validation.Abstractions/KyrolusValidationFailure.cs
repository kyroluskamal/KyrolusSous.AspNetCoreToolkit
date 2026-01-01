namespace KyrolusSous.Validation.Abstractions;

public sealed record KyrolusValidationFailure(string PropertyName, string ErrorMessage);
