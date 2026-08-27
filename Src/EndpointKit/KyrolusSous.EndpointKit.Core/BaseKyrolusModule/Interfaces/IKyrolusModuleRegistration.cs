namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;

/// <summary>
/// Defines a contract for registering module routes in the Kyrolus EndpointKit pipeline.
/// </summary>
public interface IKyrolusModuleRegistration
{
    void AddRoutes(IEndpointRouteBuilder app, IServiceProvider serviceProvider);
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusModuleRegistration"/>.
/// </summary>
public interface IModuleRegistration : IKyrolusModuleRegistration
{
}
