using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultDirectDebitMandateEngine : IKyrolusDirectDebitMandateEngine
{
    private readonly ConcurrentDictionary<string, KyrolusDirectDebitMandate> _mandates = new();

    public Task<KyrolusDirectDebitMandate> CreateMandateAsync(
        KyrolusCreateMandateRequest request,
        CancellationToken cancellationToken = default)
    {
        var mandateId = $"man_{Guid.NewGuid():N}";
        var mandateRef = $"MANDATE-{request.Scheme}-{Random.Shared.Next(100000, 999999)}";

        var mandate = new KyrolusDirectDebitMandate
        {
            MandateId = mandateId,
            CustomerId = request.CustomerId,
            MandateReference = mandateRef,
            Scheme = request.Scheme,
            Status = KyrolusMandateStatus.Active,
            Currency = request.Currency
        };

        _mandates[mandateId] = mandate;
        return Task.FromResult(mandate);
    }

    public Task<KyrolusExecuteDebitResult> ExecuteDebitAsync(
        string mandateId,
        decimal amount,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (_mandates.TryGetValue(mandateId, out var mandate) && mandate.Status == KyrolusMandateStatus.Active)
        {
            var settlementDays = mandate.Scheme switch
            {
                KyrolusDirectDebitScheme.SepaDirectDebit => 3,
                KyrolusDirectDebitScheme.UsAchDebit => 2,
                KyrolusDirectDebitScheme.EgyptIpnPull => 0,
                _ => 3
            };

            return Task.FromResult(new KyrolusExecuteDebitResult
            {
                TransactionId = $"dd_tx_{Guid.NewGuid():N}",
                MandateId = mandateId,
                Amount = amount,
                Currency = mandate.Currency,
                Succeeded = true,
                EstimatedSettlementDateUtc = DateTimeOffset.UtcNow.AddDays(settlementDays)
            });
        }

        return Task.FromResult(new KyrolusExecuteDebitResult
        {
            TransactionId = string.Empty,
            MandateId = mandateId,
            Amount = amount,
            Currency = "USD",
            Succeeded = false,
            ErrorMessage = "Mandate is not active or not found."
        });
    }

    public Task<bool> RevokeMandateAsync(string mandateId, CancellationToken cancellationToken = default)
    {
        if (_mandates.TryGetValue(mandateId, out var mandate))
        {
            _mandates[mandateId] = mandate with { Status = KyrolusMandateStatus.Revoked };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
