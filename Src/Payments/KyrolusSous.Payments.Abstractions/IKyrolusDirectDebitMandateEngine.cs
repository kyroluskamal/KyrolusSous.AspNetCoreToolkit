namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusDirectDebitMandateEngine
{
    Task<KyrolusDirectDebitMandate> CreateMandateAsync(
        KyrolusCreateMandateRequest request,
        CancellationToken cancellationToken = default);

    Task<KyrolusExecuteDebitResult> ExecuteDebitAsync(
        string mandateId,
        decimal amount,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeMandateAsync(string mandateId, CancellationToken cancellationToken = default);
}
