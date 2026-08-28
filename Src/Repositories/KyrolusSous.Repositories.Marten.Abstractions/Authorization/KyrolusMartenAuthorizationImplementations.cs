using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Authorization;

public sealed class KyrolusMartenAllowAllAuthorization : IKyrolusMartenAuthorization
{
    public static readonly IKyrolusMartenAuthorization Instance = new KyrolusMartenAllowAllAuthorization();

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

public sealed class KyrolusMartenDenyAllAuthorization : IKyrolusMartenAuthorization
{
    public static readonly IKyrolusMartenAuthorization Instance = new KyrolusMartenDenyAllAuthorization();

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

public sealed class KyrolusMartenDelegateAuthorization(Func<string, object?, CancellationToken, Task<bool>> authorize) : IKyrolusMartenAuthorization
{
    private readonly Func<string, object?, CancellationToken, Task<bool>> authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
        => authorize(operation, target, cancellationToken);
}

public sealed class KyrolusMartenOperationWhitelistAuthorization : IKyrolusMartenAuthorization
{
    private readonly HashSet<string> allowed;
    private readonly bool allowWhenUnknown;

    public KyrolusMartenOperationWhitelistAuthorization(IEnumerable<string> allowedOperations, bool allowWhenUnknown = false, StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(allowedOperations);
        allowed = new HashSet<string>(allowedOperations, comparer ?? StringComparer.OrdinalIgnoreCase);
        this.allowWhenUnknown = allowWhenUnknown;
    }

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation)) return Task.FromResult(allowWhenUnknown);
        return Task.FromResult(allowed.Contains(operation));
    }
}

public sealed class KyrolusMartenOperationBlacklistAuthorization : IKyrolusMartenAuthorization
{
    private readonly HashSet<string> blocked;
    private readonly bool allowWhenUnknown;

    public KyrolusMartenOperationBlacklistAuthorization(IEnumerable<string> blockedOperations, bool allowWhenUnknown = true, StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(blockedOperations);
        blocked = new HashSet<string>(blockedOperations, comparer ?? StringComparer.OrdinalIgnoreCase);
        this.allowWhenUnknown = allowWhenUnknown;
    }

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation)) return Task.FromResult(allowWhenUnknown);
        return Task.FromResult(!blocked.Contains(operation));
    }
}

public sealed class KyrolusMartenOperationPrefixAuthorization : IKyrolusMartenAuthorization
{
    private readonly string[] prefixes;
    private readonly bool allowWhenUnknown;
    private readonly StringComparison comparison;

    public KyrolusMartenOperationPrefixAuthorization(IEnumerable<string> prefixes, bool allowWhenUnknown = false, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        ArgumentNullException.ThrowIfNull(prefixes);
        this.prefixes = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];
        this.allowWhenUnknown = allowWhenUnknown;
        this.comparison = comparison;
    }

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation)) return Task.FromResult(allowWhenUnknown);
        return Task.FromResult(prefixes.Any(p => operation.StartsWith(p, comparison)));
    }
}

public sealed class KyrolusMartenOperationMapAuthorization(IReadOnlyDictionary<string, IKyrolusMartenAuthorization> map, IKyrolusMartenAuthorization? fallback = null) : IKyrolusMartenAuthorization
{
    private readonly IReadOnlyDictionary<string, IKyrolusMartenAuthorization> map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IKyrolusMartenAuthorization fallback = fallback ?? KyrolusMartenDenyAllAuthorization.Instance;

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation)) return fallback.AuthorizeAsync(operation, target, cancellationToken);
        if (map.TryGetValue(operation, out var auth)) return auth.AuthorizeAsync(operation, target, cancellationToken);
        return fallback.AuthorizeAsync(operation, target, cancellationToken);
    }
}

public sealed class KyrolusMartenCompositeAllAuthorization(IEnumerable<IKyrolusMartenAuthorization> rules, bool allowWhenEmpty = true) : IKyrolusMartenAuthorization
{
    private readonly IKyrolusMartenAuthorization[] rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
    private readonly bool allowWhenEmpty = allowWhenEmpty;

    public async Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (rules.Length == 0) return allowWhenEmpty;
        foreach (var rule in rules)
        {
            if (!await rule.AuthorizeAsync(operation, target, cancellationToken).ConfigureAwait(false)) return false;
        }
        return true;
    }
}

public sealed class KyrolusMartenCompositeAnyAuthorization(IEnumerable<IKyrolusMartenAuthorization> rules, bool allowWhenEmpty = false) : IKyrolusMartenAuthorization
{
    private readonly IKyrolusMartenAuthorization[] rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
    private readonly bool allowWhenEmpty = allowWhenEmpty;

    public async Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (rules.Length == 0) return allowWhenEmpty;
        foreach (var rule in rules)
        {
            if (await rule.AuthorizeAsync(operation, target, cancellationToken).ConfigureAwait(false)) return true;
        }
        return false;
    }
}

public sealed class KyrolusMartenTargetTypeAuthorization(
    IReadOnlyDictionary<Type, IKyrolusMartenAuthorization> map,
    IKyrolusMartenAuthorization? fallback = null,
    bool allowWhenNoTarget = true) : IKyrolusMartenAuthorization
{
    private readonly IReadOnlyDictionary<Type, IKyrolusMartenAuthorization> map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IKyrolusMartenAuthorization fallback = fallback ?? KyrolusMartenDenyAllAuthorization.Instance;
    private readonly bool allowWhenNoTarget = allowWhenNoTarget;

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        if (target is null) return Task.FromResult(allowWhenNoTarget);
        var type = target.GetType();
        if (map.TryGetValue(type, out var auth)) return auth.AuthorizeAsync(operation, target, cancellationToken);

        var assignable = map
            .Where(kvp => kvp.Key.IsAssignableFrom(type))
            .Select(kvp => kvp.Value)
            .FirstOrDefault();
        if (assignable is not null) return assignable.AuthorizeAsync(operation, target, cancellationToken);

        return fallback.AuthorizeAsync(operation, target, cancellationToken);
    }
}

public sealed class KyrolusMartenTenantMatchAuthorization(
    IKyrolusTenantResolver tenantResolver,
    Func<object?, string?> targetTenantResolver,
    bool allowWhenUnknown = true,
    StringComparison comparison = StringComparison.OrdinalIgnoreCase) : IKyrolusMartenAuthorization
{
    private readonly IKyrolusTenantResolver tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
    private readonly Func<object?, string?> targetTenantResolver = targetTenantResolver ?? throw new ArgumentNullException(nameof(targetTenantResolver));
    private readonly bool allowWhenUnknown = allowWhenUnknown;
    private readonly StringComparison comparison = comparison;

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        var current = tenantResolver.ResolveTenantId();
        var targetTenant = targetTenantResolver(target);
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(targetTenant))
        {
            return Task.FromResult(allowWhenUnknown);
        }

        return Task.FromResult(string.Equals(current, targetTenant, comparison));
    }
}

public sealed class KyrolusMartenRoleAuthorization : IKyrolusMartenAuthorization
{
    private readonly HashSet<string> requiredRoles;
    private readonly bool requireAll;
    private readonly bool allowWhenNoContext;
    private readonly Func<object?, IEnumerable<string>?> roleSelector;

    public KyrolusMartenRoleAuthorization(
        IEnumerable<string> requiredRoles,
        bool requireAll = false,
        bool allowWhenNoContext = false,
        Func<object?, IEnumerable<string>?>? roleSelector = null,
        StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(requiredRoles);
        this.requiredRoles = new HashSet<string>(requiredRoles, comparer ?? StringComparer.OrdinalIgnoreCase);
        this.requireAll = requireAll;
        this.allowWhenNoContext = allowWhenNoContext;
        this.roleSelector = roleSelector ?? DefaultRoleSelector;
    }

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        var roles = roleSelector(target)?.ToArray();
        if (roles is null || roles.Length == 0) return Task.FromResult(allowWhenNoContext);

        var userRoles = new HashSet<string>(roles, requiredRoles.Comparer);
        var result = requireAll
            ? requiredRoles.All(userRoles.Contains)
            : requiredRoles.Overlaps(userRoles);

        return Task.FromResult(result);
    }

    private static IEnumerable<string>? DefaultRoleSelector(object? target)
        => target is IKyrolusMartenAuthorizationContext ctx ? ctx.Roles : null;
}

public sealed class KyrolusMartenPermissionAuthorization : IKyrolusMartenAuthorization
{
    private readonly HashSet<string> requiredPermissions;
    private readonly bool requireAll;
    private readonly bool allowWhenNoContext;
    private readonly Func<object?, IEnumerable<string>?> permissionSelector;

    public KyrolusMartenPermissionAuthorization(
        IEnumerable<string> requiredPermissions,
        bool requireAll = false,
        bool allowWhenNoContext = false,
        Func<object?, IEnumerable<string>?>? permissionSelector = null,
        StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(requiredPermissions);
        this.requiredPermissions = new HashSet<string>(requiredPermissions, comparer ?? StringComparer.OrdinalIgnoreCase);
        this.requireAll = requireAll;
        this.allowWhenNoContext = allowWhenNoContext;
        this.permissionSelector = permissionSelector ?? DefaultPermissionSelector;
    }

    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
    {
        var permissions = permissionSelector(target)?.ToArray();
        if (permissions is null || permissions.Length == 0) return Task.FromResult(allowWhenNoContext);

        var userPermissions = new HashSet<string>(permissions, requiredPermissions.Comparer);
        var result = requireAll
            ? requiredPermissions.All(userPermissions.Contains)
            : requiredPermissions.Overlaps(userPermissions);

        return Task.FromResult(result);
    }

    private static IEnumerable<string>? DefaultPermissionSelector(object? target)
        => target is IKyrolusMartenAuthorizationContext ctx ? ctx.Permissions : null;
}
