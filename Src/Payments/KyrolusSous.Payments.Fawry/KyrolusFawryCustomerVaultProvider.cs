using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Fawry;

public sealed class KyrolusFawryCustomerVaultProvider : IKyrolusCustomerVaultProvider
{
    public string ProviderName => "Fawry";
    private readonly ConcurrentDictionary<string, List<KyrolusSavedPaymentMethod>> _vault = new();

    public Task<KyrolusVaultCustomer> CreateCustomerAsync(KyrolusPaymentCustomer customer, CancellationToken cancellationToken = default)
    {
        var id = customer.CustomerId ?? $"fawry_cust_{Guid.NewGuid():N}";
        _vault.TryAdd(id, []);
        return Task.FromResult(new KyrolusVaultCustomer
        {
            CustomerId = id,
            Name = customer.Name,
            Email = customer.Email,
            PhoneNumber = customer.PhoneNumber
        });
    }

    public Task<KyrolusVaultResult> SavePaymentMethodAsync(KyrolusSavePaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        var cardToken = request.PaymentTokenOrNonce;
        var list = _vault.GetOrAdd(request.CustomerId, _ => []);
        lock (list)
        {
            list.Add(new KyrolusSavedPaymentMethod
            {
                PaymentMethodId = cardToken,
                CustomerId = request.CustomerId,
                MethodType = KyrolusPaymentMethodType.CreditCard,
                LastFourDigits = "9988",
                CardBrand = "Meeza",
                IsDefault = request.SetAsDefault
            });
        }

        return Task.FromResult(new KyrolusVaultResult
        {
            Succeeded = true,
            CustomerId = request.CustomerId,
            PaymentMethodId = cardToken
        });
    }

    public Task<IReadOnlyList<KyrolusSavedPaymentMethod>> ListPaymentMethodsAsync(string customerId, CancellationToken cancellationToken = default)
    {
        if (_vault.TryGetValue(customerId, out var list))
        {
            lock (list) return Task.FromResult<IReadOnlyList<KyrolusSavedPaymentMethod>>(list.ToList().AsReadOnly());
        }
        return Task.FromResult<IReadOnlyList<KyrolusSavedPaymentMethod>>([]);
    }

    public Task<bool> DeletePaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        if (_vault.TryGetValue(customerId, out var list))
        {
            lock (list)
            {
                var item = list.FirstOrDefault(c => c.PaymentMethodId == paymentMethodId);
                if (item != null)
                {
                    list.Remove(item);
                    return Task.FromResult(true);
                }
            }
        }
        return Task.FromResult(false);
    }

    public Task<bool> SetDefaultPaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
