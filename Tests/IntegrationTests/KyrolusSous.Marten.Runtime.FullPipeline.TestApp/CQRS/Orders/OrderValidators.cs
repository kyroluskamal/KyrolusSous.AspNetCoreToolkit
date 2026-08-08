using FluentValidation;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.Orders;

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new OrderLineValidator());
    }

    private sealed class OrderLineValidator : AbstractValidator<OrderLine>
    {
        public OrderLineValidator()
        {
            RuleFor(x => x.MenuItemId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.UnitPrice).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThan(0);
        }
    }
}

public sealed class PlaceOrderCommandInterfaceValidator : AbstractValidator<IKyrolusCommand<Order>>
{
    public PlaceOrderCommandInterfaceValidator(IValidator<PlaceOrderCommand> inner)
    {
        RuleFor(x => x).Custom((command, context) =>
        {
            if (command is not PlaceOrderCommand concrete)
            {
                return;
            }

            var result = inner.Validate(concrete);
            foreach (var error in result.Errors)
            {
                context.AddFailure(error);
            }
        });
    }
}
