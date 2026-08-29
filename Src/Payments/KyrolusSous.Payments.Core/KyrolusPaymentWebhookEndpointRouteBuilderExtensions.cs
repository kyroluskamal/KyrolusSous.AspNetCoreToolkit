using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Payments.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Payments.Core;

public static class KyrolusPaymentWebhookEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapKyrolusPaymentWebhooks(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/api/payments/webhooks/{provider}")
    {
        endpoints.MapPost(pattern, async (
            string provider,
            HttpRequest request,
            IKyrolusPaymentFactory paymentFactory,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KyrolusSous.Payments.Webhooks");
            var handler = paymentFactory.GetWebhookHandler(provider);
            if (handler is null)
            {
                logger.LogWarning("No webhook handler registered for provider: {Provider}", provider);
                return Results.NotFound(new { error = $"Provider '{provider}' webhook handler not found." });
            }

            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync();

            var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            var isValid = await handler.ValidateSignatureAsync(payload, headers);
            if (!isValid)
            {
                logger.LogWarning("Invalid webhook signature for provider: {Provider}", provider);
                return Results.BadRequest(new { error = "Invalid webhook signature." });
            }

            var webhookEvent = await handler.ParseEventAsync(payload, headers);
            if (webhookEvent is null)
            {
                return Results.BadRequest(new { error = "Unable to parse webhook payload." });
            }

            // 1. Dispatch to generic IKyrolusPaymentEventHandler<T>
            var eventType = webhookEvent.GetType();
            var handlerType = typeof(IKyrolusPaymentEventHandler<>).MakeGenericType(eventType);
            var eventHandlers = serviceProvider.GetServices(handlerType);

            foreach (var eh in eventHandlers)
            {
                if (eh is not null)
                {
                    var handleMethod = handlerType.GetMethod(nameof(IKyrolusPaymentEventHandler<KyrolusWebhookEvent>.HandleAsync));
                    if (handleMethod is not null)
                    {
                        var task = (Task)handleMethod.Invoke(eh, [webhookEvent, request.HttpContext.RequestAborted])!;
                        await task.ConfigureAwait(false);
                    }
                }
            }

            // 2. Publish to IKyrolusMediator if registered in DI
            var mediator = serviceProvider.GetService<IKyrolusMediator>();
            if (mediator is not null)
            {
                if (webhookEvent.PaymentStatus == KyrolusPaymentStatus.Succeeded)
                {
                    await mediator.PublishAsync(new KyrolusPaymentSucceededNotification(
                        provider,
                        webhookEvent.TransactionId ?? string.Empty,
                        webhookEvent.Amount ?? 0,
                        webhookEvent.Currency ?? "USD",
                        OrderId: null,
                        CustomerId: null,
                        TimestampUtc: webhookEvent.TimestampUtc), request.HttpContext.RequestAborted);
                }
                else if (webhookEvent.PaymentStatus == KyrolusPaymentStatus.Failed)
                {
                    await mediator.PublishAsync(new KyrolusPaymentFailedNotification(
                        provider,
                        webhookEvent.TransactionId ?? string.Empty,
                        "Payment failed",
                        webhookEvent.Amount,
                        webhookEvent.Currency,
                        OrderId: null,
                        TimestampUtc: webhookEvent.TimestampUtc), request.HttpContext.RequestAborted);
                }
                else if (webhookEvent.PaymentStatus == KyrolusPaymentStatus.Refunded)
                {
                    await mediator.PublishAsync(new KyrolusPaymentRefundedNotification(
                        provider,
                        Guid.NewGuid().ToString("N"),
                        webhookEvent.TransactionId ?? string.Empty,
                        webhookEvent.Amount ?? 0,
                        webhookEvent.Currency ?? "USD",
                        TimestampUtc: webhookEvent.TimestampUtc), request.HttpContext.RequestAborted);
                }
            }

            return Results.Ok(new { received = true, eventId = webhookEvent.EventId, eventType = webhookEvent.EventType });
        })
        .WithTags("Payments")
        .WithName("KyrolusPaymentWebhookEndpoint");

        return endpoints;
    }
}
