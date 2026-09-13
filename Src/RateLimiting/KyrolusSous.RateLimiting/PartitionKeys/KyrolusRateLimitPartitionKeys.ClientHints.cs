namespace KyrolusSous.RateLimiting;

/// <summary>
/// Device capabilities, memory constraints, save-data preferences, and privacy signal partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on the client application platform or operating system (<c>"platform_ios"</c>, <c>"platform_android"</c>, <c>"platform_web"</c>, <c>"platform_unknown"</c>).
    /// Facilitates platform-tuned rate limiting policies accommodating intermittent mobile synchronization bursts.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The device platform partition key.</returns>
    public static string GetDevicePlatformKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-App-Platform", out var appPlat) && !string.IsNullOrWhiteSpace(appPlat))
            return $"platform_{appPlat.ToString().Trim().ToLowerInvariant()}";

        if (context.Request.Headers.TryGetValue("Sec-CH-UA-Platform", out var secPlat) && !string.IsNullOrWhiteSpace(secPlat))
        {
            var raw = secPlat.ToString().Trim().Trim('"').ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"platform_{raw}";
        }

        if (context.Request.Headers.TryGetValue("User-Agent", out var uaVal) && !string.IsNullOrWhiteSpace(uaVal))
        {
            var ua = uaVal.ToString();
            if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                return "platform_ios";
            if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
                return "platform_android";
            if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                return "platform_windows";
            if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
                return "platform_macos";
            if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                return "platform_linux";
        }

        return "platform_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the client application platform with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{platformKey}:{clientKey}"</c>.</returns>
    public static string GetDevicePlatformAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var platKey = GetDevicePlatformKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{platKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the client's data-saving preference via the <c>Save-Data</c> Client Hint header (RFC 8942 / W3C).
    /// Allows the gateway to conserve bandwidth-intensive endpoint quotas for mobile clients requesting data minimization.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"savedata_on"</c> if enabled; otherwise <c>"savedata_off"</c>.</returns>
    public static string GetClientSaveDataKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Save-Data", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var str = val.ToString().Trim();
            if (string.Equals(str, "on", StringComparison.OrdinalIgnoreCase) ||
                str == "1" ||
                str == "?1")
            {
                return "savedata_on";
            }
        }

        return "savedata_off";
    }

    /// <summary>
    /// Resolves a compound partition key combining the Save-Data client hint with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{saveDataKey}:{clientKey}"</c>.</returns>
    public static string GetClientSaveDataAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var saveKey = GetClientSaveDataKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{saveKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the Effective Connection Type (<c>ECT</c>) Network Client Hint header (W3C Network Information API).
    /// Segregates requests from constrained mobile networks (<c>"ect_slow-2g"</c>, <c>"ect_2g"</c>, <c>"ect_3g"</c>, <c>"ect_4g"</c>) to prevent socket buffer starvation on upstreams.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The ECT partition key formatted as <c>"ect_{type}"</c>, or <c>"ect_unknown"</c>.</returns>
    public static string GetEffectiveConnectionTypeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("ECT", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(raw))
                return $"ect_{raw}";
        }

        return "ect_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the Effective Connection Type (ECT) with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{ectKey}:{clientKey}"</c>.</returns>
    public static string GetEffectiveConnectionTypeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ectKey = GetEffectiveConnectionTypeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{ectKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on device RAM constraints via the W3C <c>Device-Memory</c> Client Hint header.
    /// Categorizes devices into <c>"memory_constrained"</c> (&lt;= 1 GB), <c>"memory_standard"</c> (2-4 GB), <c>"memory_high"</c> (&gt;= 8 GB), or <c>"memory_unknown"</c>.
    /// Protects gateway socket buffers from slow reading IoT and budget mobile hardware.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The device memory classification partition key.</returns>
    public static string GetDeviceMemoryBucketKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Device-Memory", out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var raw = val.ToString().Trim();
            if (double.TryParse(raw, CultureInfo.InvariantCulture, out var gb))
            {
                if (gb <= 1.0)
                    return "memory_constrained";
                if (gb <= 4.0)
                    return "memory_standard";
                return "memory_high";
            }
        }

        return "memory_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining device memory classification with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{memoryBucketKey}:{clientKey}"</c>.</returns>
    public static string GetDeviceMemoryBucketAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var memKey = GetDeviceMemoryBucketKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{memKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on user privacy preferences via Global Privacy Control (<c>Sec-GPC</c>) or Do Not Track (<c>DNT</c>).
    /// Returns <c>"privacy_gpc_enabled"</c>, <c>"privacy_dnt_enabled"</c>, or <c>"privacy_standard"</c>, isolating analytical ingestion flows for regulatory compliance.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The privacy signal partition key.</returns>
    public static string GetPrivacySignalKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Sec-GPC", out var gpcVal) && gpcVal.ToString().Trim() == "1")
            return "privacy_gpc_enabled";

        if (context.Request.Headers.TryGetValue("DNT", out var dntVal) && dntVal.ToString().Trim() == "1")
            return "privacy_dnt_enabled";

        return "privacy_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the user privacy signal with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{privacyKey}:{clientKey}"</c>.</returns>
    public static string GetPrivacySignalAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var privKey = GetPrivacySignalKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{privKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on mobile device battery saving / power mode (<c>"power_low"</c> vs <c>"power_normal"</c>).
    /// Allows the gateway to allocate lighter data budgets for battery-constrained mobile clients to preserve device longevity and backend concurrency.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"power_low"</c> if low power mode is indicated; otherwise <c>"power_normal"</c>.</returns>
    public static string GetDevicePowerStateKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if ((context.Request.Headers.TryGetValue("Save-Battery", out var bVal) && (bVal == "on" || bVal == "1")) ||
            (context.Request.Headers.TryGetValue("X-Device-Power-Mode", out var pVal) && pVal.ToString().Contains("low", StringComparison.OrdinalIgnoreCase)) ||
            (context.Request.Headers.TryGetValue("X-Power-State", out var sVal) && sVal.ToString().Contains("low", StringComparison.OrdinalIgnoreCase)))
        {
            return "power_low";
        }

        return "power_normal";
    }

    /// <summary>
    /// Resolves a compound partition key combining the device power state with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{powerKey}:{clientKey}"</c>.</returns>
    public static string GetDevicePowerStateAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var powerKey = GetDevicePowerStateKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{powerKey}:{clientKey}";
    }
}
