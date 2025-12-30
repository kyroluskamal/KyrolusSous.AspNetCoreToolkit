using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Records;

public sealed record KyrolusMartenAuthorizationContext(
    string? UserId = null,
    string? TenantId = null,
    IReadOnlyCollection<string>? Roles = null,
    IReadOnlyCollection<string>? Permissions = null) : IKyrolusMartenAuthorizationContext
{
    public IReadOnlyCollection<string> Roles { get; init; } = Roles ?? Array.Empty<string>();
    public IReadOnlyCollection<string> Permissions { get; init; } = Permissions ?? Array.Empty<string>();
}
