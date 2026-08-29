using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockCustomerVaultProvider : IKyrolusCustomerVaultProvider
{
    public string ProviderName => "Mock";

    private readonly ConcurrentDictionary<string, KyrolusVaultCustomer> _customers = new();
    private readonly ConcurrentDictionary<string, List<KyrolusSavedPaymentMethod>> _methods = new();

    public Task<KyrolusVaultCustomer> CreateCustomerAsync(KyrolusPaymentCustomer customer, CancellationToken cancellationToken = default)
    {
        var custId = customer.CustomerId ?? $"mock_cust_{Guid.NewGuid():N}";
        var vaultCust = new KyrolusVaultCustomer
        {
            CustomerId = custId,
            Name = customer.Name,
            Email = customer.Email,
            PhoneNumber = customer.PhoneNumber
        };

        _customers[custId] = vaultCust;
        _methods.TryAdd(custId, []);
        return Task.FromResult(vaultCust);
    }

    public Task<KyrolusVaultResult> SavePaymentMethodAsync(KyrolusSavePaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        var pmId = $"mock_pm_{Guid.NewGuid():N}";
        var method = new KyrolusSavedPaymentMethod
        {
            PaymentMethodId = pmId,
            CustomerId = request.CustomerId,
            MethodType = KyrolusPaymentMethodType.CreditCard,
            LastFourDigits = "4242",
            CardBrand = "Visa",
            ExpirationMonth = 12,
            ExpirationYear = 2030,
            IsDefault = request.SetAsDefault
        };

        var list = _methods.GetOrAdd(request.CustomerId, _ => []);
        lock (list)
        {
            if (request.SetAsDefault)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = list[i] with { IsDefault = false };
                }
            }
            list.Add(method);
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
        if (_methods.TryGetValue(customerId, out var list))
        {
            lock (list)
            {
                return Task.FromResult<IReadOnlyList<KyrolusSavedPaymentMethod>>(list.ToList().AsReadOnly());
            }
        }

        return Task.FromResult<IReadOnlyList<KyrolusSavedPaymentMethod>>([]);
    }

    public Task<bool> DeletePaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        if (_methods.TryGetValue(customerId, out var list))
        {
            lock (list)
            {
                var item = list.FirstOrDefault(m => m.PaymentMethodId == paymentMethodId);
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
        if (_methods.TryGetValue(customerId, out var list))
        {
            lock (list)
            {
                var found = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].PaymentMethodId == paymentMethodId)
                    {
                        list[i] = list[i] with { IsDefault = true };
                        found = true;
                    }
                    else
                    {
                        list[i] = list[i] with { IsDefault = false };
                    }
                }
                return Task.FromResult(found);
            }
        }
        return Task.FromResult(false);
    }
}
