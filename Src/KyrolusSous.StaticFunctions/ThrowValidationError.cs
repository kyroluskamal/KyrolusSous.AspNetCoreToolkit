
using FluentValidation.Results;

namespace KyrolusSous.StaticFunctions;

public static class ThrowValidationError<TEntity>
{
    public static void ThrowValidationErrors(IEnumerable<ValidationFailure> errors)
    {
        throw new FluentValidation.ValidationException("Validation error occurred", errors);
    }
}
