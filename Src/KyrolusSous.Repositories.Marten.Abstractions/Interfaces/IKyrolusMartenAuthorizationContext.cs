namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenAuthorizationContext
{
    string? UserId { get; }
    string? TenantId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
}
