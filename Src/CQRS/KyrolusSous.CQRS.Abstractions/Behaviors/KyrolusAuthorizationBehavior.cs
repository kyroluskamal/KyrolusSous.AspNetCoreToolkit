using System.Collections.Concurrent;
using System.Reflection;
using KyrolusSous.CQRS.Abstractions.Attributes;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior enforcing security and authorization rules for CQRS commands and queries.
/// </summary>
[PipelineOrder(-1000)]
public sealed class KyrolusAuthorizationBehavior<TRequest, TResponse>(
    IKyrolusCurrentUserContext? userContext = null,
    ILogger<KyrolusAuthorizationBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<KyrolusAuthorizeAttribute>> s_cachedAttributes = new();
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;
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
        ValidateAttributeAuthorization(authorizeAttributes, context);

        if (request is IAuthorizedRequest authorizedRequest)
        {
            ValidateProgrammaticAuthorization(authorizedRequest, context);
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

    private void ValidateAttributeAuthorization(
        IReadOnlyList<KyrolusAuthorizeAttribute> attributes,
        IKyrolusCurrentUserContext context)
    {
        foreach (var attr in attributes)
        {
            ValidateRoles(attr.Roles, context);
            ValidatePermissions(attr.Permissions, context);
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

    private static void ValidateProgrammaticAuthorization(IAuthorizedRequest request, IKyrolusCurrentUserContext context)
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
    }
}
