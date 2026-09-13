using Microsoft.AspNetCore.Http;

namespace KyrolusSous.RateLimiting;

/// <summary>
/// Internet of Things (IoT), Connected Vehicle Telematics, Hardware Fleets, Firmware OTA Rollout, and Sensor Telemetry partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    private static readonly string[] DefaultFirmwareHeaders =
    [
        "X-Firmware-Version",
        "X-Device-Firmware",
        "X-OTA-Version"
    ];

    /// <summary>
    /// Resolves a partition key based on the client device firmware or Over-The-Air (OTA) build version (<c>"fw_v2.4.1"</c>, <c>"fw_unspecified"</c>).
    /// Mitigates Thundering Herd synchronization spikes when millions of distributed IoT edge devices wake up simultaneously to report state following a firmware rollout.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="firmwareHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The firmware rollout partition key formatted as <c>"fw_{version}"</c>, or <c>"fw_unspecified"</c>.</returns>
    public static string GetFirmwareRolloutGroupKey(HttpContext context, string[]? firmwareHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = firmwareHeaderCandidates is { Length: > 0 } ? firmwareHeaderCandidates : DefaultFirmwareHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"fw_{SanitizeSegment(raw)}";
            }
        }

        return "fw_unspecified";
    }

    /// <summary>
    /// Resolves a compound partition key combining the firmware rollout version with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="firmwareHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{firmwareKey}:{clientKey}"</c>.</returns>
    public static string GetFirmwareRolloutGroupAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? firmwareHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fwKey = GetFirmwareRolloutGroupKey(context, firmwareHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{fwKey}:{clientKey}";
    }

    private static readonly string[] DefaultHardwareFleetHeaders =
    [
        "X-Hardware-Model",
        "X-Device-Type",
        "X-Fleet-Type"
    ];

    /// <summary>
    /// Resolves a partition key based on the connected hardware device model or fleet category (<c>"fleet_smart_meter"</c>, <c>"fleet_vehicle_telematics"</c>, <c>"fleet_pos_terminal"</c>).
    /// Segregates mission-critical industrial telemetry from consumer smart home devices.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="fleetHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The hardware fleet category partition key formatted as <c>"fleet_{model}"</c>, or <c>"fleet_unknown"</c>.</returns>
    public static string GetHardwareFleetCategoryKey(HttpContext context, string[]? fleetHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = fleetHeaderCandidates is { Length: > 0 } ? fleetHeaderCandidates : DefaultHardwareFleetHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"fleet_{SanitizeSegment(raw)}";
            }
        }

        return "fleet_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the hardware fleet category with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="fleetHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{fleetKey}:{clientKey}"</c>.</returns>
    public static string GetHardwareFleetCategoryAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? fleetHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fleetKey = GetHardwareFleetCategoryKey(context, fleetHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{fleetKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the sensor telemetry reporting frequency mode (<c>"telemetry_realtime"</c>, <c>"telemetry_power_save"</c>, <c>"telemetry_standard"</c>).
    /// Enables higher burst allowances for emergency realtime vehicle collision/alert signals while enforcing strict quotas on low-power periodic heartbeats.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The telemetry reporting frequency mode partition key.</returns>
    public static string GetTelemetryFrequencyModeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-Telemetry-Mode", out var val) ||
            context.Request.Headers.TryGetValue("X-Reporting-Interval", out val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (raw.Contains("realtime") || raw.Contains("instant") || raw.Contains("emergency") || raw.Contains("high"))
                return "telemetry_realtime";
            if (raw.Contains("powersave") || raw.Contains("power_save") || raw.Contains("battery") || raw.Contains("low"))
                return "telemetry_power_save";
        }

        return "telemetry_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the telemetry frequency mode with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{telemetryModeKey}:{clientKey}"</c>.</returns>
    public static string GetTelemetryFrequencyModeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modeKey = GetTelemetryFrequencyModeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{modeKey}:{clientKey}";
    }
}
