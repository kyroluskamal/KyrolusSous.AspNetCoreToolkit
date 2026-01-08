namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
public interface IModuleRegistration
{
    void AddRoutes(IEndpointRouteBuilder app, IServiceProvider serviceProvider);
}
