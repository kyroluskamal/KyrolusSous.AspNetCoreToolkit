using Microsoft.VisualStudio.TestPlatform;

namespace KyrolusSous.Validation.DataAnnotations.Unitests;

public class ValidationDataAnnotationTestsHelper
{
    public static ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TestUserRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var validator = new DataAnnotationsRequestValidator<TestUserRequest>();
        return validator.ValidateAsync(request, context, cancellationToken);
    }
}
