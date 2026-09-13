using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class SecurityPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Geo location resolvers (Country, City, Continent) resolve accurately")]
    public void Geo_Location_Resolvers_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Country
        KyrolusRateLimitPartitionKeys.GetGeoCountryKey(context).ShouldBe("country_unknown");
        context.Request.Headers["CF-IPCountry"] = "eg";
        KyrolusRateLimitPartitionKeys.GetGeoCountryKey(context).ShouldBe("country_EG");
        KyrolusRateLimitPartitionKeys.GetGeoCountryAndClientKey(context).ShouldBe("country_EG:198.51.100.42");

        // City
        var cityContext = new DefaultHttpContext();
        cityContext.Connection.RemoteIpAddress = BaseIp;
        cityContext.Request.Headers["CF-IPCity"] = "Alexandria";
        KyrolusRateLimitPartitionKeys.GetGeoCityKey(cityContext).ShouldBe("city_alexandria");
        KyrolusRateLimitPartitionKeys.GetGeoCityAndClientKey(cityContext).ShouldBe("city_alexandria:198.51.100.42");

        // Continent
        var contContext = new DefaultHttpContext();
        contContext.Connection.RemoteIpAddress = BaseIp;
        contContext.Request.Headers["CF-Continent"] = "AF";
        KyrolusRateLimitPartitionKeys.GetGeoContinentKey(contContext).ShouldBe("continent_AF");
        KyrolusRateLimitPartitionKeys.GetGeoContinentAndClientKey(contContext).ShouldBe("continent_AF:198.51.100.42");
    }

    [Fact(DisplayName = "ASN, AS-Org, CDN PoP, and IP Type resolve accurately")]
    public void Asn_Org_Pop_And_IpType_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // ASN
        context.Request.Headers["CF-Connecting-ASN"] = "13335";
        KyrolusRateLimitPartitionKeys.GetAsnKey(context).ShouldBe("asn_13335");
        KyrolusRateLimitPartitionKeys.GetAsnAndClientKey(context).ShouldBe("asn_13335:198.51.100.42");

        // AS-Org
        var orgContext = new DefaultHttpContext();
        orgContext.Connection.RemoteIpAddress = BaseIp;
        orgContext.Request.Headers["CF-IP-Organization"] = "Cloudflare Inc.";
        KyrolusRateLimitPartitionKeys.GetAsOrganizationKey(orgContext).ShouldBe("asorg_cloudflare_inc");
        KyrolusRateLimitPartitionKeys.GetAsOrganizationAndClientKey(orgContext).ShouldBe("asorg_cloudflare_inc:198.51.100.42");

        // CDN PoP
        var popContext = new DefaultHttpContext();
        popContext.Connection.RemoteIpAddress = BaseIp;
        popContext.Request.Headers["CF-Ray"] = "8f123456789-AMS";
        KyrolusRateLimitPartitionKeys.GetCdnPopKey(popContext).ShouldBe("pop_ams");
        KyrolusRateLimitPartitionKeys.GetCdnPopAndClientKey(popContext).ShouldBe("pop_ams:198.51.100.42");

        // IP Type
        var ipTypeContext = new DefaultHttpContext();
        ipTypeContext.Connection.RemoteIpAddress = BaseIp;
        ipTypeContext.Request.Headers["CF-IP-Type"] = "datacenter";
        KyrolusRateLimitPartitionKeys.GetIpTypeKey(ipTypeContext).ShouldBe("iptype_datacenter");
        KyrolusRateLimitPartitionKeys.GetIpTypeAndClientKey(ipTypeContext).ShouldBe("iptype_datacenter:198.51.100.42");
    }

    [Fact(DisplayName = "TLS Fingerprint, Protocol Version, and Resumption resolve accurately")]
    public void Tls_Fingerprint_Version_And_Resumption_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // TLS JA3/JA4 Fingerprint
        context.Request.Headers["CF-JA3-Hash"] = "cd08e31494f9531f560d64cfa39e75f0";
        KyrolusRateLimitPartitionKeys.GetTlsFingerprintKey(context).ShouldBe("tlsfp_cd08e31494f9531f560d64cfa39e75f0");

        // TLS Protocol Version
        var tlsContext = new DefaultHttpContext();
        tlsContext.Connection.RemoteIpAddress = BaseIp;
        tlsContext.Request.Headers["X-SSL-Protocol"] = "TLSv1.3";
        KyrolusRateLimitPartitionKeys.GetTlsProtocolVersionKey(tlsContext).ShouldBe("tls_1_3");
        KyrolusRateLimitPartitionKeys.GetTlsProtocolVersionAndClientKey(tlsContext).ShouldBe("tls_1_3:198.51.100.42");

        // TLS Resumption
        var resumeContext = new DefaultHttpContext();
        resumeContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetTlsResumptionKey(resumeContext).ShouldBe("tls_full_handshake");

        resumeContext.Request.Headers["X-SSL-Session-Reused"] = "true";
        KyrolusRateLimitPartitionKeys.GetTlsResumptionKey(resumeContext).ShouldBe("tls_resumed");
        KyrolusRateLimitPartitionKeys.GetTlsResumptionAndClientKey(resumeContext).ShouldBe("tls_resumed:198.51.100.42");
    }

    [Fact(DisplayName = "Bot Threat Score, Client Fingerprint, and Early Data resolve accurately")]
    public void BotScore_Fingerprint_And_EarlyData_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Bot Threat Score
        context.Request.Headers["CF-Bot-Score"] = "1";
        KyrolusRateLimitPartitionKeys.GetBotThreatScoreKey(context).ShouldBe("botscore_automated");
        KyrolusRateLimitPartitionKeys.GetBotThreatScoreAndClientKey(context).ShouldBe("botscore_automated:198.51.100.42");

        // Client Device Fingerprint
        var fpContext1 = new DefaultHttpContext();
        fpContext1.Connection.RemoteIpAddress = BaseIp;
        fpContext1.Request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        fpContext1.Request.Headers["Accept-Language"] = "en-US,en;q=0.9";
        fpContext1.Request.Headers["Sec-CH-UA"] = "\"Chromium\";v=\"120\"";
        fpContext1.Request.Headers["Sec-CH-UA-Platform"] = "\"Windows\"";

        var fpKey1 = KyrolusRateLimitPartitionKeys.GetClientFingerprintKey(fpContext1);
        fpKey1.ShouldStartWith("fingerprint_");

        var fpContext2 = new DefaultHttpContext();
        fpContext2.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.199");
        fpContext2.Request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        fpContext2.Request.Headers["Accept-Language"] = "en-US,en;q=0.9";
        fpContext2.Request.Headers["Sec-CH-UA"] = "\"Chromium\";v=\"120\"";
        fpContext2.Request.Headers["Sec-CH-UA-Platform"] = "\"Windows\"";

        KyrolusRateLimitPartitionKeys.GetClientFingerprintKey(fpContext2).ShouldBe(fpKey1);

        // Early Data 0-RTT
        var edContext = new DefaultHttpContext();
        edContext.Connection.RemoteIpAddress = BaseIp;
        edContext.Request.Headers["Early-Data"] = "1";
        KyrolusRateLimitPartitionKeys.GetEarlyDataKey(edContext).ShouldBe("earlydata_0rtt");
        KyrolusRateLimitPartitionKeys.GetEarlyDataAndClientKey(edContext).ShouldBe("earlydata_0rtt:198.51.100.42");
    }

    [Fact(DisplayName = "Idempotency Key, Timestamp Skew, File Extensions, Client Automation, and WAF Threat Score resolve accurately")]
    public void Idempotency_Skew_FileRisk_Automation_And_WafThreat_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Idempotency Key
        context.Request.Headers["Idempotency-Key"] = "fin_tx_7b8b54b0";
        KyrolusRateLimitPartitionKeys.GetIdempotencyKey(context).ShouldBe("idempotency_fin_tx_7b8b54b0");

        // Timestamp Skew
        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FakeTimeProvider(now);
        var skewContext = new DefaultHttpContext();
        skewContext.Connection.RemoteIpAddress = BaseIp;
        skewContext.Request.Headers["X-Signature-Timestamp"] = now.AddSeconds(-10).ToUnixTimeSeconds().ToString();
        KyrolusRateLimitPartitionKeys.GetTimestampSkewKey(skewContext, timeProvider: fixedTimeProvider).ShouldBe("skew_normal");
        KyrolusRateLimitPartitionKeys.GetTimestampSkewAndClientKey(skewContext, timeProvider: fixedTimeProvider).ShouldBe("skew_normal:198.51.100.42");

        // Uploaded File Extension Risk
        var exeFormContext = new DefaultHttpContext();
        exeFormContext.Connection.RemoteIpAddress = BaseIp;
        exeFormContext.Request.ContentType = "multipart/form-data";
        var exeFiles = new FormFileCollection { new FormFile(Stream.Null, 0, 0, "f", "payload.exe") };
        exeFormContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), exeFiles);
        KyrolusRateLimitPartitionKeys.GetUploadedFileExtensionKey(exeFormContext).ShouldBe("ext_executable");
        KyrolusRateLimitPartitionKeys.GetUploadedFileExtensionAndClientKey(exeFormContext).ShouldBe("ext_executable:198.51.100.42");

        // Client Automation
        var uaContext = new DefaultHttpContext();
        uaContext.Connection.RemoteIpAddress = BaseIp;
        uaContext.Request.Headers["User-Agent"] = "curl/8.1.2";
        KyrolusRateLimitPartitionKeys.GetHeaderOrderingAnomalyKey(uaContext).ShouldBe("client_automation");
        KyrolusRateLimitPartitionKeys.GetHeaderOrderingAnomalyAndClientKey(uaContext).ShouldBe("client_automation:198.51.100.42");

        // WAF IP Threat Score
        var threatContext = new DefaultHttpContext();
        threatContext.Connection.RemoteIpAddress = BaseIp;
        threatContext.Request.Headers["CF-Threat-Score"] = "30";
        KyrolusRateLimitPartitionKeys.GetIpReputationScoreKey(threatContext).ShouldBe("threat_low");
        KyrolusRateLimitPartitionKeys.GetIpReputationScoreAndClientKey(threatContext).ShouldBe("threat_low:198.51.100.42");
    }
}
