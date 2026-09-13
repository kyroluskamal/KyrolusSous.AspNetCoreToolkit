using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class IotPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Firmware Rollout Group, Hardware Fleet Category, and Telemetry Mode resolve accurately")]
    public void Iot_Keys_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Firmware Rollout Group
        KyrolusRateLimitPartitionKeys.GetFirmwareRolloutGroupKey(context).ShouldBe("fw_unspecified");
        context.Request.Headers["X-Firmware-Version"] = "v3.12.4-rc1";
        KyrolusRateLimitPartitionKeys.GetFirmwareRolloutGroupKey(context).ShouldBe("fw_v3.12.4-rc1");
        KyrolusRateLimitPartitionKeys.GetFirmwareRolloutGroupAndClientKey(context).ShouldBe("fw_v3.12.4-rc1:198.51.100.42");

        // Hardware Fleet Category
        var fleetContext = new DefaultHttpContext();
        fleetContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetHardwareFleetCategoryKey(fleetContext).ShouldBe("fleet_unknown");

        fleetContext.Request.Headers["X-Hardware-Model"] = "Telematics-Gateway-T900";
        KyrolusRateLimitPartitionKeys.GetHardwareFleetCategoryKey(fleetContext).ShouldBe("fleet_telematics-gateway-t900");
        KyrolusRateLimitPartitionKeys.GetHardwareFleetCategoryAndClientKey(fleetContext).ShouldBe("fleet_telematics-gateway-t900:198.51.100.42");

        // Telemetry Reporting Mode
        var telemContext = new DefaultHttpContext();
        telemContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetTelemetryFrequencyModeKey(telemContext).ShouldBe("telemetry_standard");

        telemContext.Request.Headers["X-Telemetry-Mode"] = "realtime-crash-alert";
        KyrolusRateLimitPartitionKeys.GetTelemetryFrequencyModeKey(telemContext).ShouldBe("telemetry_realtime");
        KyrolusRateLimitPartitionKeys.GetTelemetryFrequencyModeAndClientKey(telemContext).ShouldBe("telemetry_realtime:198.51.100.42");

        telemContext.Request.Headers["X-Telemetry-Mode"] = "powersave_battery";
        KyrolusRateLimitPartitionKeys.GetTelemetryFrequencyModeKey(telemContext).ShouldBe("telemetry_power_save");
    }
}
