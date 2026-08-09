using System.Linq.Expressions;
using System.Security.Claims;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Authorization;

public sealed record KyrolusMartenAuthorizationContext<TResponse>(
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

public sealed record KyrolusMartenAuthorizationResult<TResponse>(
    bool IsAuthorized = true,
    string? ErrorMessage = null,
    bool ReturnNotFound = true,
    Expression<Func<TResponse, bool>>? RowFilter = null,
    IReadOnlyCollection<string>? AllowedFields = null,
    IReadOnlyCollection<string>? AllowedIncludes = null,
    IReadOnlyCollection<string>? AllowedPatchProperties = null)
    where TResponse : class;

public interface IKyrolusMartenAuthorizationProvider<TResponse>
    where TResponse : class
{
    ValueTask<KyrolusMartenAuthorizationResult<TResponse>> AuthorizeAsync(
        KyrolusMartenAuthorizationContext<TResponse> context,
        CancellationToken cancellationToken = default);
}

public sealed class KyrolusNoopMartenAuthorizationProvider<TResponse> : IKyrolusMartenAuthorizationProvider<TResponse>
    where TResponse : class
{
    public static IKyrolusMartenAuthorizationProvider<TResponse> Instance { get; } = new KyrolusNoopMartenAuthorizationProvider<TResponse>();

    public ValueTask<KyrolusMartenAuthorizationResult<TResponse>> AuthorizeAsync(
        KyrolusMartenAuthorizationContext<TResponse> context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new KyrolusMartenAuthorizationResult<TResponse>());
}
