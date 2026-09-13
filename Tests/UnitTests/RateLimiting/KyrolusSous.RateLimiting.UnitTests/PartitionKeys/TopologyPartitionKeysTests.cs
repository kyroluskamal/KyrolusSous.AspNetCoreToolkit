using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class TopologyPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Database Replica Intent, Data Residency, and Tenant Physical Shard resolve accurately")]
    public void Topology_Keys_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // DB Replica Intent
        context.Request.Method = "GET";
        KyrolusRateLimitPartitionKeys.GetDatabaseReplicaIntentKey(context).ShouldBe("db_replica");
        KyrolusRateLimitPartitionKeys.GetDatabaseReplicaIntentAndClientKey(context).ShouldBe("db_replica:198.51.100.42");

        context.Request.Method = "POST";
        KyrolusRateLimitPartitionKeys.GetDatabaseReplicaIntentKey(context).ShouldBe("db_primary");

        context.Request.Headers["X-Database-Target"] = "replica";
        KyrolusRateLimitPartitionKeys.GetDatabaseReplicaIntentKey(context).ShouldBe("db_replica");

        // Data Residency
        var resContext = new DefaultHttpContext();
        resContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetDataResidencyKey(resContext).ShouldBe("residency_global");

        resContext.Request.Headers["X-Data-Residency"] = "eu-gdpr";
        KyrolusRateLimitPartitionKeys.GetDataResidencyKey(resContext).ShouldBe("residency_eu-gdpr");
        KyrolusRateLimitPartitionKeys.GetDataResidencyAndClientKey(resContext).ShouldBe("residency_eu-gdpr:198.51.100.42");

        // Tenant Database Shard
        var shardContext = new DefaultHttpContext();
        shardContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetTenantDatabaseShardKey(shardContext).ShouldBe("dbshard_default");

        shardContext.Request.Headers["X-Tenant-Shard"] = "cluster_db_07";
        KyrolusRateLimitPartitionKeys.GetTenantDatabaseShardKey(shardContext).ShouldBe("dbshard_cluster_db_07");
        KyrolusRateLimitPartitionKeys.GetTenantDatabaseShardAndClientKey(shardContext).ShouldBe("dbshard_cluster_db_07:198.51.100.42");
    }
}
