namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusValidationErrorCodeMapper
{
    string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context);
}

public interface IKyrolusValidationFieldPathMapper
{
    string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context);
}
