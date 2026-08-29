namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusCustomerVaultProvider
{
    string ProviderName { get; }

    Task<KyrolusVaultCustomer> CreateCustomerAsync(KyrolusPaymentCustomer customer, CancellationToken cancellationToken = default);
    Task<KyrolusVaultResult> SavePaymentMethodAsync(KyrolusSavePaymentMethodRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusSavedPaymentMethod>> ListPaymentMethodsAsync(string customerId, CancellationToken cancellationToken = default);
    Task<bool> DeletePaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultPaymentMethodAsync(string customerId, string paymentMethodId, CancellationToken cancellationToken = default);
}
