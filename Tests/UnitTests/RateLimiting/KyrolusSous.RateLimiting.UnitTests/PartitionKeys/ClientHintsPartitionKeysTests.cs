using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class ClientHintsPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Device Platform and Device Memory resolve accurately")]
    public void DevicePlatform_And_DeviceMemory_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Platform
        KyrolusRateLimitPartitionKeys.GetDevicePlatformKey(context).ShouldBe("platform_unknown");

        context.Request.Headers["X-App-Platform"] = "iOS";
        KyrolusRateLimitPartitionKeys.GetDevicePlatformKey(context).ShouldBe("platform_ios");
        KyrolusRateLimitPartitionKeys.GetDevicePlatformAndClientKey(context).ShouldBe("platform_ios:198.51.100.42");

        context.Request.Headers["X-App-Platform"] = "Android";
        KyrolusRateLimitPartitionKeys.GetDevicePlatformKey(context).ShouldBe("platform_android");

        // Device Memory
        var memContext = new DefaultHttpContext();
        memContext.Connection.RemoteIpAddress = BaseIp;
        memContext.Request.Headers["Device-Memory"] = "0.5";
        KyrolusRateLimitPartitionKeys.GetDeviceMemoryBucketKey(memContext).ShouldBe("memory_constrained");
        KyrolusRateLimitPartitionKeys.GetDeviceMemoryBucketAndClientKey(memContext).ShouldBe("memory_constrained:198.51.100.42");

        memContext.Request.Headers["Device-Memory"] = "4";
        KyrolusRateLimitPartitionKeys.GetDeviceMemoryBucketKey(memContext).ShouldBe("memory_standard");

        memContext.Request.Headers["Device-Memory"] = "16";
        KyrolusRateLimitPartitionKeys.GetDeviceMemoryBucketKey(memContext).ShouldBe("memory_high");
    }

    [Fact(DisplayName = "Save-Data, ECT, Device Power State, and Privacy Signals resolve accurately")]
    public void SaveData_Ect_PowerState_And_Privacy_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Save-Data
        context.Request.Headers["Save-Data"] = "on";
        KyrolusRateLimitPartitionKeys.GetClientSaveDataKey(context).ShouldBe("savedata_on");
        KyrolusRateLimitPartitionKeys.GetClientSaveDataAndClientKey(context).ShouldBe("savedata_on:198.51.100.42");

        // ECT Network Quality
        var ectContext = new DefaultHttpContext();
        ectContext.Connection.RemoteIpAddress = BaseIp;
        ectContext.Request.Headers["ECT"] = "3g";
        KyrolusRateLimitPartitionKeys.GetEffectiveConnectionTypeKey(ectContext).ShouldBe("ect_3g");
        KyrolusRateLimitPartitionKeys.GetEffectiveConnectionTypeAndClientKey(ectContext).ShouldBe("ect_3g:198.51.100.42");

        // Device Power State
        var powerContext = new DefaultHttpContext();
        powerContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetDevicePowerStateKey(powerContext).ShouldBe("power_normal");

        powerContext.Request.Headers["Save-Battery"] = "on";
        KyrolusRateLimitPartitionKeys.GetDevicePowerStateKey(powerContext).ShouldBe("power_low");
        KyrolusRateLimitPartitionKeys.GetDevicePowerStateAndClientKey(powerContext).ShouldBe("power_low:198.51.100.42");

        // Privacy Signals
        var privacyContext = new DefaultHttpContext();
        privacyContext.Connection.RemoteIpAddress = BaseIp;
        privacyContext.Request.Headers["Sec-GPC"] = "1";
        KyrolusRateLimitPartitionKeys.GetPrivacySignalKey(privacyContext).ShouldBe("privacy_gpc_enabled");
        KyrolusRateLimitPartitionKeys.GetPrivacySignalAndClientKey(privacyContext).ShouldBe("privacy_gpc_enabled:198.51.100.42");

        privacyContext.Request.Headers.Remove("Sec-GPC");
        privacyContext.Request.Headers["DNT"] = "1";
        KyrolusRateLimitPartitionKeys.GetPrivacySignalKey(privacyContext).ShouldBe("privacy_dnt_enabled");
    }
}
