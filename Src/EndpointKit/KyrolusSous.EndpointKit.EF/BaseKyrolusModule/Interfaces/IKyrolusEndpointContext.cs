namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

public interface IKyrolusEndpointContext
{
    string? TenantId { get; }
    string? ScopeKey { get; }
}
