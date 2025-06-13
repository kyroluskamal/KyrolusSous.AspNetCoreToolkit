namespace KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;
public interface IModuleRegistration
{
    void AddRoutes(IEndpointRouteBuilder app, IServiceProvider serviceProvider);
}