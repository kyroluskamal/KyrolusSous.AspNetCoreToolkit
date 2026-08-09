using System.Linq.Expressions;
using System.Security.Claims;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Authorization;

public sealed record KyrolusEfAuthorizationContext<TResponse>(
    EndpointNames Endpoint,
    string? HttpMethod,
    string? Route,
    ClaimsPrincipal? User,
    HttpContext? HttpContext,
    string? TenantId,
    string? ScopeKey,
    IReadOnlyCollection<string>? RequestedFields,
    IReadOnlyCollection<string>? RequestedIncludes,
    IReadOnlyCollection<string>? RequestedPatchProperties,
    object? ResourceId,
    object?[]? KeyValues)
    where TResponse : class;

public sealed record KyrolusEfAuthorizationResult<TResponse>(
    bool IsAuthorized = true,
    string? ErrorMessage = null,
    bool ReturnNotFound = true,
    Expression<Func<TResponse, bool>>? RowFilter = null,
    IReadOnlyCollection<string>? AllowedFields = null,
    IReadOnlyCollection<string>? AllowedIncludes = null,
    IReadOnlyCollection<string>? AllowedPatchProperties = null)
    where TResponse : class;

public interface IKyrolusEfAuthorizationProvider<TResponse>
    where TResponse : class
{
    ValueTask<KyrolusEfAuthorizationResult<TResponse>> AuthorizeAsync(
        KyrolusEfAuthorizationContext<TResponse> context,
        CancellationToken cancellationToken = default);
}

public sealed class KyrolusNoopEfAuthorizationProvider<TResponse> : IKyrolusEfAuthorizationProvider<TResponse>
    where TResponse : class
{
    public static IKyrolusEfAuthorizationProvider<TResponse> Instance { get; } = new KyrolusNoopEfAuthorizationProvider<TResponse>();

    public ValueTask<KyrolusEfAuthorizationResult<TResponse>> AuthorizeAsync(
        KyrolusEfAuthorizationContext<TResponse> context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new KyrolusEfAuthorizationResult<TResponse>());
}
