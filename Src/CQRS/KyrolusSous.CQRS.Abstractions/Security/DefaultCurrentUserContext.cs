namespace KyrolusSous.CQRS.Abstractions.Security;

/// <summary>
/// Default implementation of <see cref="IKyrolusCurrentUserContext"/> backed by a <see cref="ClaimsPrincipal"/> or ambient service provider.
/// </summary>
public class KyrolusDefaultCurrentUserContext : IKyrolusCurrentUserContext
{
    private readonly ClaimsPrincipal? _user;
    private readonly Lazy<HashSet<string>> _roles;
    private readonly Lazy<HashSet<string>> _permissions;

    public KyrolusDefaultCurrentUserContext(ClaimsPrincipal? user = null, string? tenantId = null)
    {
        _user = user;
        TenantId = tenantId ?? user?.FindFirst("tenant_id")?.Value ?? user?.FindFirst("tenant")?.Value;

        _roles = new Lazy<HashSet<string>>(() =>
        {
            if (_user is null) return [];
            return _user.FindAll(ClaimTypes.Role)
                .Concat(_user.FindAll("role"))
                .Concat(_user.FindAll("roles"))
                .Concat(_user.FindAll("http://schemas.microsoft.com/ws/2008/06/identity/claims/role"))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        });

        _permissions = new Lazy<HashSet<string>>(() =>
        {
            if (_user is null) return [];
            return _user.FindAll("permission")
                .Concat(_user.FindAll("permissions"))
                .Concat(_user.FindAll("scope"))
                .Concat(_user.FindAll("scp"))
                .Concat(_user.FindAll("http://schemas.microsoft.com/identity/claims/scope"))
                .SelectMany(c => c.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        });
    }

    public KyrolusDefaultCurrentUserContext(IServiceProvider? serviceProvider)
        : this(TryExtractUser(serviceProvider), TryExtractTenant(serviceProvider))
    {
    }

    private static ClaimsPrincipal? TryExtractUser(IServiceProvider? serviceProvider)
    {
        if (serviceProvider is null) return null;
        try
        {
            var httpContextAccessorType = Type.GetType("Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.Abstractions")
                ?? Type.GetType("Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.Features");
            if (httpContextAccessorType is null) return null;

            var accessor = serviceProvider.GetService(httpContextAccessorType);
            if (accessor is null) return null;

            var httpContextProp = accessor.GetType().GetProperty("HttpContext");
            var httpContext = httpContextProp?.GetValue(accessor);
            if (httpContext is null) return null;

            var userProp = httpContext.GetType().GetProperty("User");
            return userProp?.GetValue(httpContext) as ClaimsPrincipal;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractTenant(IServiceProvider? serviceProvider)
    {
        var user = TryExtractUser(serviceProvider);
        return user?.FindFirst("tenant_id")?.Value ?? user?.FindFirst("tenant")?.Value;
    }

    /// <inheritdoc />
    public string? UserId => _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? _user?.FindFirst("sub")?.Value;

    /// <inheritdoc />
    public string? UserName => _user?.Identity?.Name ?? _user?.FindFirst("name")?.Value ?? _user?.FindFirst(ClaimTypes.Name)?.Value;

    /// <inheritdoc />
    public string? TenantId { get; }

    /// <inheritdoc />
    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public ClaimsPrincipal? User => _user;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles => _roles.Value;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Permissions => _permissions.Value;

    /// <inheritdoc />
    public bool IsInRole(string role) => _roles.Value.Contains(role);

    /// <inheritdoc />
    public bool HasPermission(string permission) => _permissions.Value.Contains(permission);
}
