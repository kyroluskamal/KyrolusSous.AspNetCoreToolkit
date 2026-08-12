
namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusValidationHook
{
    ValueTask OnBeforeAsync(
        object? request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    ValueTask OnAfterAsync(
        object? request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default);
}

public interface IKyrolusValidationHook<in TRequest>
{
    ValueTask OnBeforeAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    ValueTask OnAfterAsync(
        TRequest request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default);
}
