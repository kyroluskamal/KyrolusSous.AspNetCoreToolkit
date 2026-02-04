using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.Orders;

public sealed record PlaceOrderCommand(
    string CustomerEmail,
    IReadOnlyList<OrderLine> Lines,
    string PaymentMethod) : IKyrolusCommand<Order>;

public sealed record GetOrderByIdQuery(Guid OrderId) : IKyrolusQuery<Order?>;
