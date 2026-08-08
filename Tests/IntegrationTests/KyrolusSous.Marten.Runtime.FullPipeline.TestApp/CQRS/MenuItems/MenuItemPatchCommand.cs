using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.MenuItems;

public sealed class MenuItemPatchCommand : IKyrolusCommand<MenuItem>
{
    public Guid Id { get; set; }
    public Dictionary<string, object> Updates { get; set; } = new();
    public string? TenantId { get; set; }
    public string? RowVersionPropertyName { get; set; }
}
