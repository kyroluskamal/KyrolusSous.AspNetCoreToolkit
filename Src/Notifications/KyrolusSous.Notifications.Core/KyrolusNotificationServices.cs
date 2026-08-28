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
/// Resilient dispatcher executing email/sms delivery with automatic fallback provider failover and error logging.
/// </summary>
public sealed class KyrolusResilientNotificationDispatcher(
    IEnumerable<IKyrolusEmailSender> emailSenders,
    IEnumerable<IKyrolusSmsSender> smsSenders,
    ILogger<KyrolusResilientNotificationDispatcher>? logger = null) : IKyrolusNotificationDispatcher
{
    private readonly IReadOnlyList<IKyrolusEmailSender> _emailSenders = emailSenders.ToList();
    private readonly IReadOnlyList<IKyrolusSmsSender> _smsSenders = smsSenders.ToList();
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
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusNotifications(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusTemplateRenderer, KyrolusTemplateRenderer>();
        services.AddSingleton<IKyrolusNotificationDispatcher, KyrolusResilientNotificationDispatcher>();
        return services;
    }
}
