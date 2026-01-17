namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;

public interface IKyrolusEndpointContext
{
    string? TenantId { get; }
    string? ScopeKey { get; }
}
