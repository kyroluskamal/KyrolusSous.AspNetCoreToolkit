using FluentValidation;
using KyrolusSous.CQRS.Marten.Command.Add;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.MenuItems;

public sealed class AddMenuItemValidator : AbstractValidator<AddCommand<MenuItem>>
{
    public AddMenuItemValidator()
    {
        RuleFor(x => x.Entity).NotNull();
        RuleFor(x => x.Entity.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Entity.Category).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Entity.Price).GreaterThan(0);
    }
}

public sealed class UpdateMenuItemValidator : AbstractValidator<UpdateCommand<MenuItem>>
{
    public UpdateMenuItemValidator()
    {
        RuleFor(x => x.Entity).NotNull();
        RuleFor(x => x.Entity.Id).NotEmpty();
        RuleFor(x => x.Entity.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Entity.Category).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Entity.Price).GreaterThan(0);
    }
}

public sealed class PatchMenuItemValidator : AbstractValidator<MenuItemPatchCommand>
{
    public PatchMenuItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Updates).NotNull();
    }
}

public sealed class MenuItemValidator : AbstractValidator<MenuItem>
{
    public MenuItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
