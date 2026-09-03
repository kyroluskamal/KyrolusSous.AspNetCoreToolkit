namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior enforcing security and authorization rules for CQRS commands and queries.
/// </summary>
[PipelineOrder(-1050)]
public sealed class KyrolusAuthorizationBehavior<TRequest, TResponse>(
    IKyrolusCurrentUserContext? userContext = null,
    IKyrolusAuthorizationPolicyEvaluator? policyEvaluator = null,
    ILogger<KyrolusAuthorizationBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<KyrolusAuthorizeAttribute>> s_cachedAttributes = new();
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;
    private readonly IKyrolusAuthorizationPolicyEvaluator? _policyEvaluator = policyEvaluator;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var authorizeAttributes = s_cachedAttributes.GetOrAdd(
            typeof(TRequest),
            static type => type.GetCustomAttributes<KyrolusAuthorizeAttribute>(inherit: true).ToArray());

        var isProgrammatic = request is IAuthorizedRequest;

        if (authorizeAttributes.Count == 0 && !isProgrammatic)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var context = _userContext ?? new KyrolusDefaultCurrentUserContext();
        ValidateAuthentication(context);
        await ValidateAttributeAuthorizationAsync(authorizeAttributes, context, request!, cancellationToken).ConfigureAwait(false);

        if (request is IAuthorizedRequest authorizedRequest)
        {
            await ValidateProgrammaticAuthorizationAsync(authorizedRequest, context, request, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }

    private void ValidateAuthentication(IKyrolusCurrentUserContext context)
    {
        if (!context.IsAuthenticated)
        {
            _logger?.LogWarning("[Kyrolus CQRS Security] Unauthenticated request for {RequestType}", typeof(TRequest).Name);
            throw new KyrolusSecurityException($"User is not authenticated to execute '{typeof(TRequest).Name}'.");
        }
    }

    private async Task ValidateAttributeAuthorizationAsync(
        IReadOnlyList<KyrolusAuthorizeAttribute> attributes,
        IKyrolusCurrentUserContext context,
        object request,
        CancellationToken cancellationToken)
    {
        foreach (var attr in attributes)
        {
            ValidateRoles(attr.Roles, context);
            ValidatePermissions(attr.Permissions, context);
            await ValidatePolicyAsync(attr.Policy, context, request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Evaluates a named policy via <see cref="IKyrolusAuthorizationPolicyEvaluator"/>.
    /// </summary>
    /// <remarks>
    /// Fails closed: a request naming a policy with no evaluator registered throws immediately rather
    /// than silently letting the request through, because <c>Policy</c>/<c>RequiredPolicy</c> being
    /// declared but never checked used to be exactly that - a silent authorization bypass for anyone
    /// who reached for the policy option instead of roles/permissions.
    /// </remarks>
    private async Task ValidatePolicyAsync(string? policyName, IKyrolusCurrentUserContext context, object request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(policyName)) return;

        if (_policyEvaluator is null)
        {
            throw new InvalidOperationException(
                $"[Kyrolus CQRS Security] '{typeof(TRequest).Name}' requires policy '{policyName}', but no " +
                $"{nameof(IKyrolusAuthorizationPolicyEvaluator)} is registered. Register one (for example a " +
                "bridge to ASP.NET Core's IAuthorizationService) before using policy-based authorization - " +
                "a request naming a policy must never execute without that policy actually being evaluated.");
        }

        var satisfied = await _policyEvaluator.EvaluateAsync(policyName, context, request, cancellationToken).ConfigureAwait(false);
        if (!satisfied)
        {
            _logger?.LogWarning(
                "[Kyrolus CQRS Security] User '{UserId}' does not satisfy policy '{Policy}' for {RequestType}",
                context.UserId,
                policyName,
                typeof(TRequest).Name);
            throw new KyrolusSecurityException(
                $"User '{context.UserId}' does not satisfy the required policy '{policyName}'.",
                policyName);
        }
    }

    private void ValidateRoles(string? rolesString, IKyrolusCurrentUserContext context)
    {
        if (string.IsNullOrWhiteSpace(rolesString)) return;

        var allowedRoles = rolesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowedRoles.Length > 0 && !allowedRoles.Any(context.IsInRole))
        {
            _logger?.LogWarning(
                "[Kyrolus CQRS Security] User '{UserId}' lacks required role(s) [{Roles}] for {RequestType}",
                context.UserId,
                rolesString,
                typeof(TRequest).Name);
            throw new KyrolusSecurityException(
                $"User '{context.UserId}' lacks one of the required roles: {rolesString}.",
                rolesString);
        }
    }

    private void ValidatePermissions(string? permissionsString, IKyrolusCurrentUserContext context)
    {
        if (string.IsNullOrWhiteSpace(permissionsString)) return;

        var missingPermission = permissionsString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(perm => !context.HasPermission(perm));

        if (missingPermission is not null)
        {
            _logger?.LogWarning(
                "[Kyrolus CQRS Security] User '{UserId}' lacks required permission '{Permission}' for {RequestType}",
                context.UserId,
                missingPermission,
                typeof(TRequest).Name);
            throw new KyrolusSecurityException(
                $"User '{context.UserId}' lacks the required permission '{missingPermission}'.",
                missingPermission);
        }
    }

    private async Task ValidateProgrammaticAuthorizationAsync(
        IAuthorizedRequest request,
        IKyrolusCurrentUserContext context,
        object rawRequest,
        CancellationToken cancellationToken)
    {
        if (request.RequiredRoles is { Count: > 0 } roles && !roles.Any(context.IsInRole))
        {
            throw new KyrolusSecurityException(
                $"User lacks required role from request contract: {string.Join(',', roles)}.");
        }

        if (request.RequiredPermissions is { Count: > 0 } permissions)
        {
            var missingPermission = permissions.FirstOrDefault(perm => !context.HasPermission(perm));
            if (missingPermission is not null)
            {
                throw new KyrolusSecurityException(
                    $"User lacks required permission from request contract: '{missingPermission}'.",
                    missingPermission);
            }
        }

        await ValidatePolicyAsync(request.RequiredPolicy, context, rawRequest, cancellationToken).ConfigureAwait(false);
    }
}
