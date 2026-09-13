using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.CQRS.ExceptionHandling;

/// <summary>
/// Opt-in <see cref="IKyrolusExceptionMapper{TResponse}"/> base class that recognizes this toolkit's
/// own well-known CQRS-pipeline exceptions - <see cref="KyrolusSecurityException"/>,
/// <see cref="KyrolusIdempotencyConflictException"/>, and <see cref="KyrolusValidationException"/> -
/// and delegates turning each one into a <typeparamref name="TResponse"/> to a dedicated abstract
/// factory method: this class only decides which category an exception falls into, the app decides
/// what its own <typeparamref name="TResponse"/> looks like for that category.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ServiceCollectionExtensions.AddKyrolusCqrsExceptionHandling"/> registers
/// <see cref="KyrolusExceptionMappingBehavior{TRequest,TResponse}"/> with an empty
/// <c>IKyrolusExceptionMapper&lt;TResponse&gt;</c> list by default, precisely because
/// <typeparamref name="TResponse"/> is unconstrained and a generic mapper cannot construct an
/// arbitrary response type from "here's an exception" alone. That default is unchanged by this class
/// and remains correct for an app that wants unhandled exceptions to propagate by design. What this
/// class adds is narrower and genuinely generic-safe: this toolkit already knows what
/// <see cref="KyrolusSecurityException"/>, <see cref="KyrolusIdempotencyConflictException"/>, and
/// <see cref="KyrolusValidationException"/> each mean, so an app that subclasses this instead of
/// implementing <see cref="IKyrolusExceptionMapper{TResponse}"/> from scratch gets those three
/// recognized consistently without repeating the same <see langword="switch"/>/<see langword="is"/>
/// pattern-matching in every app. An app with fully custom mapping needs - including one that wants to
/// treat these same exception types differently, or that wants zero mappers registered at all - can
/// still implement <see cref="IKyrolusExceptionMapper{TResponse}"/> directly; nothing here is required.
/// </para>
/// <para>
/// An exception type this class does not recognize makes <see cref="TryMap"/> return
/// <see langword="false"/>, exactly like any other unmatched exception passed to an
/// <see cref="IKyrolusExceptionMapper{TResponse}"/> - never a fallback "something went wrong" response
/// synthesized without real information - so <see cref="KyrolusExceptionMappingBehavior{TRequest,TResponse}"/>
/// keeps trying the next registered mapper, or lets the original exception propagate if none match.
/// Recognizing anything beyond these three would mean guessing at exceptions this toolkit's own CQRS
/// pipeline does not actually throw, which is exactly the unfounded assumption this class exists to avoid.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The response type this mapper produces when it recognizes an exception.</typeparam>
public abstract class KyrolusExceptionMapperBase<TResponse> : IKyrolusExceptionMapper<TResponse>
{
    public bool TryMap(Exception exception, out TResponse response)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case KyrolusSecurityException securityException:
                response = OnSecurityException(securityException);
                return true;

            case KyrolusIdempotencyConflictException idempotencyConflictException:
                response = OnIdempotencyConflict(idempotencyConflictException);
                return true;

            case KyrolusValidationException validationException:
                response = OnValidationFailed(validationException);
                return true;

            default:
                response = default!;
                return false;
        }
    }

    /// <summary>
    /// Produces the <typeparamref name="TResponse"/> for a request rejected by a security check -
    /// <c>KyrolusAuthorizationBehavior</c>, <c>KyrolusTenantScopingBehavior</c>,
    /// <c>KyrolusPropertyAllowListBehavior</c>, and other guards across this toolkit all signal their
    /// failure the same way, by throwing <see cref="KyrolusSecurityException"/>.
    /// </summary>
    protected abstract TResponse OnSecurityException(KyrolusSecurityException exception);

    /// <summary>
    /// Produces the <typeparamref name="TResponse"/> for a command whose idempotency key is already
    /// claimed by another in-flight execution. See <see cref="KyrolusIdempotencyConflictException"/>'s
    /// own remarks - the correct response here is usually "retry shortly", never "the command failed".
    /// </summary>
    protected abstract TResponse OnIdempotencyConflict(KyrolusIdempotencyConflictException exception);

    /// <summary>
    /// Produces the <typeparamref name="TResponse"/> for a request rejected by
    /// <c>KyrolusValidationBehavior</c>/<c>KyrolusBatchValidationBehavior</c> because one or more
    /// validators, or the validation engine, reported failures at or above the configured blocking
    /// severity. <see cref="KyrolusValidationException.Errors"/> carries the individual failures.
    /// </summary>
    protected abstract TResponse OnValidationFailed(KyrolusValidationException exception);
}
