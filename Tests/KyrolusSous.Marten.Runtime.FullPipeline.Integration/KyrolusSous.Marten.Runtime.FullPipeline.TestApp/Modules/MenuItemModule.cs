using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Modules;

public sealed class MenuItemModule(
    IRouteMapper<MenuItem, MenuItem, Guid> routeMapper,
    IKyrolusApiConfig<MenuItem> config)
    : BaseKyrolusModule<MenuItem, MenuItem, Guid>(routeMapper, config)
{
}
