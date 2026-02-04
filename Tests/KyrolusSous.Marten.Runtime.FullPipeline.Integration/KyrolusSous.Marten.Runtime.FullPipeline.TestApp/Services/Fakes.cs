namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;

public sealed record EmailMessage(string To, string Subject, string Body, DateTimeOffset SentAt);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> messages = [];

    public IReadOnlyList<EmailMessage> Messages => messages;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        messages.Add(message);
        return Task.CompletedTask;
    }
}

public sealed record PaymentRequest(Guid OrderId, decimal Amount, string Currency, string PaymentMethod, string? TenantId);
public sealed record PaymentResult(bool Succeeded, string? Reference, string? FailureReason);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}

public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly List<PaymentRequest> requests = [];
    public IReadOnlyList<PaymentRequest> Requests => requests;

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        requests.Add(request);
        if (string.Equals(request.PaymentMethod, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new PaymentResult(false, null, "Payment declined"));
        }

        var reference = $"PAY-{request.OrderId:N}";
        return Task.FromResult(new PaymentResult(true, reference, null));
    }
}
