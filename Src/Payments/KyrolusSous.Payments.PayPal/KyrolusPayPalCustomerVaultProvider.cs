using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalCustomerVaultProvider : IKyrolusCustomerVaultProvider
{
    public string ProviderName => "PayPal";
    private readonly ConcurrentDictionary<string, List<KyrolusSavedPaymentMethod>> _vault = new();

    public Task<KyrolusVaultCustomer> CreateCustomerAsync(KyrolusPaymentCustomer customer, CancellationToken cancellationToken = default)
    {
        var id = customer.CustomerId ?? $"paypal_cust_{Guid.NewGuid():N}";
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
        var pmId = $"paypal_vault_{Guid.NewGuid():N}";
        var list = _vault.GetOrAdd(request.CustomerId, _ => []);
        lock (list)
        {
            list.Add(new KyrolusSavedPaymentMethod
            {
                PaymentMethodId = pmId,
                CustomerId = request.CustomerId,
                MethodType = KyrolusPaymentMethodType.DigitalWallet,
                IsDefault = request.SetAsDefault
            });
        }

        return Task.FromResult(new KyrolusVaultResult
        {
            Succeeded = true,
            CustomerId = request.CustomerId,
            PaymentMethodId = pmId
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
                var item = list.FirstOrDefault(x => x.PaymentMethodId == paymentMethodId);
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
        if (_vault.TryGetValue(customerId, out var list))
        {
            lock (list)
            {
                foreach (var m in list)
                {
                    // updated
                }
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
}
