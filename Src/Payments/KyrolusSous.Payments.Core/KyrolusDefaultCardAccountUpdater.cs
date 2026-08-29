using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultCardAccountUpdater : IKyrolusCardAccountUpdater
{
    public Task<KyrolusAccountUpdateResult> CheckForUpdatesAsync(
        KyrolusAccountUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var currentCardExpiry = new DateTime(request.CurrentExpiryYear, request.CurrentExpiryMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        // If card expired, simulate VAU/ABU renewing expiry by +3 years
        if (now >= currentCardExpiry)
        {
            return Task.FromResult(new KyrolusAccountUpdateResult
            {
                PaymentMethodId = request.PaymentMethodId,
                Action = KyrolusAccountUpdateAction.UpdatedExpiry,
                NewLast4 = request.CurrentLast4,
                NewExpiryMonth = request.CurrentExpiryMonth,
                NewExpiryYear = request.CurrentExpiryYear + 3
            });
        }

        return Task.FromResult(new KyrolusAccountUpdateResult
        {
            PaymentMethodId = request.PaymentMethodId,
            Action = KyrolusAccountUpdateAction.NoChange
        });
    }
}
