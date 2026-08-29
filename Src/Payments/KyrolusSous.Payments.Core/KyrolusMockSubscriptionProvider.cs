using System.Collections.Concurrent;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockSubscriptionProvider : IKyrolusSubscriptionProvider
{
    public string ProviderName => "Mock";

    private readonly ConcurrentDictionary<string, KyrolusSubscriptionPlan> _plans = new();
    private readonly ConcurrentDictionary<string, KyrolusSubscriptionResult> _subscriptions = new();

    public Task<KyrolusSubscriptionPlan> CreatePlanAsync(KyrolusSubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        _plans[plan.PlanId] = plan;
        return Task.FromResult(plan);
    }

    public Task<KyrolusSubscriptionResult> CreateSubscriptionAsync(KyrolusSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var subId = $"mock_sub_{Guid.NewGuid():N}";
        var plan = _plans.TryGetValue(request.PlanId, out var p) ? p : null;

        var sub = new KyrolusSubscriptionResult
        {
            SubscriptionId = subId,
            CustomerId = request.CustomerId,
            PlanId = request.PlanId,
            Status = KyrolusSubscriptionStatus.Active,
            Amount = plan?.Amount ?? 29.99m,
            Currency = plan?.Currency ?? "USD",
            CurrentPeriodStartUtc = DateTimeOffset.UtcNow,
            CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddMonths(1)
        };

        _subscriptions[subId] = sub;
        return Task.FromResult(sub);
    }

    public Task<KyrolusSubscriptionResult> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var sub))
        {
            return Task.FromResult(sub);
        }

        return Task.FromResult(new KyrolusSubscriptionResult
        {
            SubscriptionId = subscriptionId,
            Status = KyrolusSubscriptionStatus.Cancelled,
            ErrorMessage = "Subscription not found."
        });
    }

    public Task<KyrolusSubscriptionResult> CancelSubscriptionAsync(string subscriptionId, bool cancelImmediately = false, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var sub))
        {
            var updated = sub with { Status = KyrolusSubscriptionStatus.Cancelled, CancelAtPeriodEnd = !cancelImmediately };
            _subscriptions[subscriptionId] = updated;
            return Task.FromResult(updated);
        }

        return Task.FromResult(new KyrolusSubscriptionResult
        {
            SubscriptionId = subscriptionId,
            Status = KyrolusSubscriptionStatus.Cancelled,
            ErrorMessage = "Subscription not found."
        });
    }

    public Task<KyrolusSubscriptionResult> PauseSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var sub))
        {
            var updated = sub with { Status = KyrolusSubscriptionStatus.Paused };
            _subscriptions[subscriptionId] = updated;
            return Task.FromResult(updated);
        }

        return Task.FromResult(new KyrolusSubscriptionResult
        {
            SubscriptionId = subscriptionId,
            Status = KyrolusSubscriptionStatus.Cancelled,
            ErrorMessage = "Subscription not found."
        });
    }

    public Task<KyrolusSubscriptionResult> ResumeSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var sub))
        {
            var updated = sub with { Status = KyrolusSubscriptionStatus.Active };
            _subscriptions[subscriptionId] = updated;
            return Task.FromResult(updated);
        }

        return Task.FromResult(new KyrolusSubscriptionResult
        {
            SubscriptionId = subscriptionId,
            Status = KyrolusSubscriptionStatus.Cancelled,
            ErrorMessage = "Subscription not found."
        });
    }
}
