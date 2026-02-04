using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using Marten;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.Orders;

public sealed class PlaceOrderHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver,
    IPaymentGateway paymentGateway,
    IEmailSender emailSender)
    : IKyrolusCommandHandler<PlaceOrderCommand, Order>
{
    public async Task<Order> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var tenant = tenantResolver.ResolveTenantId() ?? string.Empty;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            CustomerEmail = command.CustomerEmail,
            Lines = command.Lines.ToList(),
            Total = command.Lines.Sum(l => l.UnitPrice * l.Quantity),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = OrderStatus.Pending
        };

        var paymentResult = await paymentGateway.ChargeAsync(
            new PaymentRequest(order.Id, order.Total, "USD", command.PaymentMethod, tenant),
            cancellationToken).ConfigureAwait(false);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            OrderId = order.Id,
            Amount = order.Total,
            Status = paymentResult.Succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            ProviderReference = paymentResult.Reference
        };

        order.Status = paymentResult.Succeeded ? OrderStatus.Paid : OrderStatus.Failed;

        var orderRepo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, Order, Guid>>();
        var paymentRepo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, Payment, Guid>>();
        await orderRepo.AddAsync(order, cancellationToken).ConfigureAwait(false);
        await paymentRepo.AddAsync(payment, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!paymentResult.Succeeded)
        {
            throw new KyrolusExternalServiceException("Payment failed", paymentResult.FailureReason ?? "Payment declined");
        }

        await emailSender.SendAsync(
            new EmailMessage(order.CustomerEmail, "Order confirmed", $"Order {order.Id} confirmed", DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return order;
    }
}

public sealed class GetOrderByIdHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver)
    : IKyrolusQueryHandler<GetOrderByIdQuery, Order?>
{
    public async Task<Order?> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, Order, Guid>>();
        var tenant = tenantResolver.ResolveTenantId();
        var result = await repo.GetByIdAsync(query.OrderId, new(TenantId: tenant), cancellationToken).ConfigureAwait(false);
        return result?.Entity;
    }
}
