namespace KyrolusSous.CQRS.Abstractions.Security;

/// <summary>
/// Evaluates a named authorization policy (<see cref="Attributes.KyrolusAuthorizeAttribute.Policy"/> /
/// <see cref="Interfaces.IAuthorizedRequest.RequiredPolicy"/>) against the current user and request.
/// </summary>
/// <remarks>
/// Implement this to bridge into whatever policy engine the host application already has - most
/// commonly <c>Microsoft.AspNetCore.Authorization.IAuthorizationService</c>, but it could just as
/// well be a custom rules engine. <see cref="KyrolusSous.CQRS.Abstractions.Behaviors.KyrolusAuthorizationBehavior{TRequest,TResponse}"/>
/// never guesses at policy semantics itself; if a request names a policy and no evaluator is
/// registered, authorization fails closed with a clear configuration error instead of silently
/// letting the request through.
/// </remarks>
public interface IKyrolusAuthorizationPolicyEvaluator
{
    /// <summary>
    /// Evaluates whether <paramref name="context"/> satisfies <paramref name="policyName"/> for
    /// <paramref name="request"/>.
    /// </summary>
    /// <param name="policyName">The policy name from the attribute or request contract.</param>
    /// <param name="context">The current user/security context.</param>
    /// <param name="request">The request being authorized, in case the policy is request-sensitive (e.g. resource-based authorization).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the policy is satisfied.</returns>
    Task<bool> EvaluateAsync(
        string policyName,
        IKyrolusCurrentUserContext context,
        object request,
        CancellationToken cancellationToken);
}
