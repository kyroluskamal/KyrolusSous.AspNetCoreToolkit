using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockPaymentLinkProvider : IKyrolusPaymentLinkProvider
{
    public string ProviderName => "Mock";
    private readonly ConcurrentDictionary<string, KyrolusPaymentLinkResult> _links = new();

    public Task<KyrolusPaymentLinkResult> CreatePaymentLinkAsync(KyrolusPaymentLinkRequest request, CancellationToken cancellationToken = default)
    {
        var linkId = $"plink_{Guid.NewGuid():N}";
        var url = $"https://pay.kyrolus.test/pay/{linkId}";
        var qr = $"kyrolus:pay:{linkId}:{request.Amount}:{request.Currency}";

        var result = new KyrolusPaymentLinkResult
        {
            LinkId = linkId,
            Url = url,
            QrCodePayload = qr,
            ReferenceCode = $"REF-{Random.Shared.Next(100000, 999999)}",
            Amount = request.Amount,
            Currency = request.Currency,
            ExpiresAtUtc = request.ExpiresIn.HasValue ? DateTimeOffset.UtcNow.Add(request.ExpiresIn.Value) : null,
            IsActive = true
        };

        _links[linkId] = result;
        return Task.FromResult(result);
    }

    public Task<bool> DeactivatePaymentLinkAsync(string linkId, CancellationToken cancellationToken = default)
    {
        if (_links.TryGetValue(linkId, out var link))
        {
            _links[linkId] = link with { IsActive = false };
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
