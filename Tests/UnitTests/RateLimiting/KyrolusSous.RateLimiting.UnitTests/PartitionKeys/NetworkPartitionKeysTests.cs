using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class NetworkPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "ClientIp resolves remote IP or trusted forwarded headers")]
    public void ClientIp_ResolvesRemoteIpOrTrustedForwardedHeaders()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        KyrolusRateLimitPartitionKeys.GetClientIpKey(context).ShouldBe("198.51.100.42");

        // Forwarded header without trusted proxy validation
        context.Request.Headers["CF-Connecting-IP"] = "203.0.113.195";
        KyrolusRateLimitPartitionKeys.GetClientIpKey(context, "CF-Connecting-IP").ShouldBe("203.0.113.195");

        // Forwarded header with multiple comma-separated IPs (e.g. X-Forwarded-For)
        context.Request.Headers["X-Forwarded-For"] = "192.0.2.1, 10.0.0.1";
        KyrolusRateLimitPartitionKeys.GetClientIpKey(context, "X-Forwarded-For").ShouldBe("192.0.2.1");

        // Trusted proxy validation: allowed
        KyrolusRateLimitPartitionKeys.GetClientIpKey(context, "CF-Connecting-IP", ip => ip.Equals(BaseIp)).ShouldBe("203.0.113.195");

        // Trusted proxy validation: rejected (spoofing defense)
        KyrolusRateLimitPartitionKeys.GetClientIpKey(context, "CF-Connecting-IP", _ => false).ShouldBe("198.51.100.42");

        var nullIpContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetClientIpKey(nullIpContext).ShouldBe("unknown_ip");
    }

    [Fact(DisplayName = "SubnetKey computes IPv4 and IPv6 CIDR prefix masking correctly")]
    public void SubnetKey_ComputesCidrPrefixMaskingCorrectly()
    {
        var ipv4Context = new DefaultHttpContext();
        ipv4Context.Connection.RemoteIpAddress = BaseIp;

        KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv4Context).ShouldBe("subnet_198.51.100.0/24");
        KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv4Context, ipv4PrefixLength: 16).ShouldBe("subnet_198.51.0.0/16");

        var ipv6Context = new DefaultHttpContext();
        ipv6Context.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8:abcd:0012::1");
        KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv6Context).ShouldBe("subnet_2001:db8:abcd:12::/64");

        var mappedIpv6 = new DefaultHttpContext();
        mappedIpv6.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:198.51.100.42");
        KyrolusRateLimitPartitionKeys.GetSubnetKey(mappedIpv6).ShouldBe("subnet_198.51.100.0/24");

        var nullContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetSubnetKey(nullContext).ShouldBe("unknown_ip");

        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv4Context, ipv4PrefixLength: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv4Context, ipv4PrefixLength: 33));
        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv4Context, ipv6PrefixLength: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRateLimitPartitionKeys.GetSubnetKey(ipv4Context, ipv6PrefixLength: 129));
    }

    [Fact(DisplayName = "ServerPort and ForwardedPort partition local and edge listeners correctly")]
    public void ServerPort_And_ForwardedPort_PartitionAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.Connection.LocalPort = 8443;

        KyrolusRateLimitPartitionKeys.GetServerPortKey(context).ShouldBe("port_8443");
        KyrolusRateLimitPartitionKeys.GetServerPortAndClientKey(context).ShouldBe("port_8443:198.51.100.42");

        KyrolusRateLimitPartitionKeys.GetForwardedPortKey(context).ShouldBe("fwdport_8443");
        context.Request.Headers["X-Forwarded-Port"] = "443";
        KyrolusRateLimitPartitionKeys.GetForwardedPortKey(context).ShouldBe("fwdport_443");
        KyrolusRateLimitPartitionKeys.GetForwardedPortAndClientKey(context).ShouldBe("fwdport_443:198.51.100.42");
    }

    [Fact(DisplayName = "ConnectionId and Scheme partition transport sockets correctly")]
    public void ConnectionId_And_Scheme_PartitionAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.Connection.Id = "conn_xyz_12345";
        context.Request.Scheme = "https";

        KyrolusRateLimitPartitionKeys.GetConnectionIdKey(context).ShouldBe("conn_conn_xyz_12345");
        KyrolusRateLimitPartitionKeys.GetConnectionIdAndClientKey(context).ShouldBe("conn_conn_xyz_12345:198.51.100.42");

        KyrolusRateLimitPartitionKeys.GetSchemeKey(context).ShouldBe("scheme_https");
        KyrolusRateLimitPartitionKeys.GetSchemeAndClientKey(context).ShouldBe("scheme_https:198.51.100.42");

        var emptyContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetConnectionIdKey(emptyContext).ShouldBe("conn_unknown");
    }
}
