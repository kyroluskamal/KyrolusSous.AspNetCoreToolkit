using System.Reflection;
using System.Text.RegularExpressions;
using KyrolusSous.Notifications.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Notifications.Core;

/// <summary>
/// High-performance template rendering engine supporting placeholder substitution {{PropertyName}} and basic conditional blocks {{#if Property}}...{{/if}}.
/// </summary>
public sealed class KyrolusTemplateRenderer : IKyrolusTemplateRenderer
{
    private static readonly Regex VariableRegex = new(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex ConditionRegex = new(@"\{\{#if\s+([a-zA-Z0-9_]+)\}\}(.*?)\{\{/if\}\}", RegexOptions.Compiled | RegexOptions.Singleline);

    public Task<string> RenderAsync(string templateContent, object model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(templateContent) || model is null)
        {
            return Task.FromResult(templateContent ?? string.Empty);
        }

        var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.GetValue(model), StringComparer.OrdinalIgnoreCase);

        // Process conditional blocks
        var processed = ConditionRegex.Replace(templateContent, match =>
        {
            var propName = match.Groups[1].Value;
            var innerContent = match.Groups[2].Value;

            if (properties.TryGetValue(propName, out var val))
            {
                if (val is bool b && b) return innerContent;
                if (val is not null and not false and not 0 and not "") return innerContent;
            }

            return string.Empty;
        });

        // Process variable substitutions
        processed = VariableRegex.Replace(processed, match =>
        {
            var propName = match.Groups[1].Value;
            if (properties.TryGetValue(propName, out var val) && val is not null)
            {
                return val.ToString() ?? string.Empty;
            }

            return match.Value; // Keep unmatched tokens intact
        });

        return Task.FromResult(processed);
    }
}

/// <summary>
/// Resilient dispatcher executing email/sms/push delivery with automatic fallback provider failover and error logging.
/// </summary>
public sealed class KyrolusResilientNotificationDispatcher(
    IEnumerable<IKyrolusEmailSender> emailSenders,
    IEnumerable<IKyrolusSmsSender> smsSenders,
    IEnumerable<IKyrolusPushSender> pushSenders,
    ILogger<KyrolusResilientNotificationDispatcher>? logger = null) : IKyrolusNotificationDispatcher
{
    private readonly IReadOnlyList<IKyrolusEmailSender> _emailSenders = emailSenders.ToList();
    private readonly IReadOnlyList<IKyrolusSmsSender> _smsSenders = smsSenders.ToList();
    private readonly IReadOnlyList<IKyrolusPushSender> _pushSenders = pushSenders.ToList();
    private readonly ILogger<KyrolusResilientNotificationDispatcher>? _logger = logger;

    public async Task<KyrolusNotificationResult> DispatchEmailAsync(KyrolusEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_emailSenders.Count == 0)
        {
            return KyrolusNotificationResult.Failure("No email senders are registered in the DI container.");
        }

        var errors = new List<string>();
        foreach (var sender in _emailSenders)
        {
            try
            {
                var result = await sender.SendEmailAsync(message, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    return result;
                }

                errors.Add($"[{sender.GetType().Name}]: {result.ErrorMessage}");
                _logger?.LogWarning("Email delivery failed on primary provider {Sender}: {Error}. Attempting next fallback.", sender.GetType().Name, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                errors.Add($"[{sender.GetType().Name}]: {ex.Message}");
                _logger?.LogError(ex, "Exception sending email on provider {Sender}.", sender.GetType().Name);
            }
        }

        return KyrolusNotificationResult.Failure($"All {errors.Count} email providers failed: {string.Join(" | ", errors)}");
    }

    public async Task<KyrolusNotificationResult> DispatchSmsAsync(KyrolusSmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_smsSenders.Count == 0)
        {
            return KyrolusNotificationResult.Failure("No SMS senders are registered in the DI container.");
        }

        var errors = new List<string>();
        foreach (var sender in _smsSenders)
        {
            try
            {
                var result = await sender.SendSmsAsync(message, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    return result;
                }

                errors.Add($"[{sender.GetType().Name}]: {result.ErrorMessage}");
                _logger?.LogWarning("SMS delivery failed on provider {Sender}: {Error}. Attempting next fallback.", sender.GetType().Name, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                errors.Add($"[{sender.GetType().Name}]: {ex.Message}");
                _logger?.LogError(ex, "Exception sending SMS on provider {Sender}.", sender.GetType().Name);
            }
        }

        return KyrolusNotificationResult.Failure($"All {errors.Count} SMS providers failed: {string.Join(" | ", errors)}");
    }

    public async Task<KyrolusNotificationResult> DispatchPushAsync(KyrolusPushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_pushSenders.Count == 0)
        {
            return KyrolusNotificationResult.Failure("No Push notification senders are registered in the DI container.");
        }

        var errors = new List<string>();
        foreach (var sender in _pushSenders)
        {
            try
            {
                var result = await sender.SendPushAsync(message, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    return result;
                }

                errors.Add($"[{sender.GetType().Name}]: {result.ErrorMessage}");
                _logger?.LogWarning("Push delivery failed on provider {Sender}: {Error}. Attempting next fallback.", sender.GetType().Name, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                errors.Add($"[{sender.GetType().Name}]: {ex.Message}");
                _logger?.LogError(ex, "Exception sending Push on provider {Sender}.", sender.GetType().Name);
            }
        }

        return KyrolusNotificationResult.Failure($"All {errors.Count} Push providers failed: {string.Join(" | ", errors)}");
    }
}

/// <summary>
/// In-memory storage for user in-app notifications.
/// </summary>
public sealed class KyrolusInMemoryInAppNotificationStore : IKyrolusInAppNotificationStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<KyrolusInAppNotification>> _userNotifications = new(StringComparer.OrdinalIgnoreCase);

    public Task<KyrolusInAppNotification> SaveNotificationAsync(KyrolusInAppNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var list = _userNotifications.GetOrAdd(notification.UserId, _ => []);
        lock (list)
        {
            list.Add(notification);
        }
        return Task.FromResult(notification);
    }

    public Task<IReadOnlyList<KyrolusInAppNotification>> GetNotificationsAsync(string userId, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        if (!_userNotifications.TryGetValue(userId, out var list))
        {
            return Task.FromResult<IReadOnlyList<KyrolusInAppNotification>>([]);
        }

        lock (list)
        {
            var query = unreadOnly ? list.Where(n => !n.IsRead) : list;
            return Task.FromResult<IReadOnlyList<KyrolusInAppNotification>>(query.OrderByDescending(n => n.CreatedAt).ToList());
        }
    }

    public Task<bool> MarkAsReadAsync(string notificationId, string userId, CancellationToken cancellationToken = default)
    {
        if (!_userNotifications.TryGetValue(userId, out var list))
        {
            return Task.FromResult(false);
        }

        lock (list)
        {
            var item = list.FirstOrDefault(n => n.Id == notificationId);
            if (item != null)
            {
                var idx = list.IndexOf(item);
                list[idx] = item with { IsRead = true };
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_userNotifications.TryGetValue(userId, out var list))
        {
            return Task.FromResult(0);
        }

        lock (list)
        {
            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (!list[i].IsRead)
                {
                    list[i] = list[i] with { IsRead = true };
                    count++;
                }
            }
            return Task.FromResult(count);
        }
    }
}

/// <summary>
/// In-memory notification queue.
/// </summary>
public sealed class KyrolusInMemoryNotificationQueue(IKyrolusNotificationDispatcher dispatcher) : IKyrolusNotificationQueue
{
    private readonly IKyrolusNotificationDispatcher _dispatcher = dispatcher;

    public Task EnqueueEmailAsync(KyrolusEmailMessage message, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () => await _dispatcher.DispatchEmailAsync(message, CancellationToken.None).ConfigureAwait(false), cancellationToken);
        return Task.CompletedTask;
    }

    public Task EnqueueSmsAsync(KyrolusSmsMessage message, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () => await _dispatcher.DispatchSmsAsync(message, CancellationToken.None).ConfigureAwait(false), cancellationToken);
        return Task.CompletedTask;
    }

    public Task EnqueuePushAsync(KyrolusPushMessage message, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () => await _dispatcher.DispatchPushAsync(message, CancellationToken.None).ConfigureAwait(false), cancellationToken);
        return Task.CompletedTask;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusNotifications(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusTemplateRenderer, KyrolusTemplateRenderer>();
        services.AddSingleton<IKyrolusNotificationDispatcher, KyrolusResilientNotificationDispatcher>();
        services.AddSingleton<IKyrolusInAppNotificationStore, KyrolusInMemoryInAppNotificationStore>();
        services.AddSingleton<IKyrolusNotificationQueue, KyrolusInMemoryNotificationQueue>();
        return services;
    }
}
