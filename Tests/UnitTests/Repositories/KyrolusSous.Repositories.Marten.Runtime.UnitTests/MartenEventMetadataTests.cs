using KyrolusSous.Repositories.Marten.Abstractions.Metadata;
using KyrolusSous.Repositories.Marten.Runtime.Metadata;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenEventMetadataTests
{
    [Fact(DisplayName = "MetadataProvider: Enriches metadata dictionary with ambient context")]
    public void GetMetadata_IncludesContextHeadersAndTimestamp()
    {
        var context = new KyrolusMartenEventMetadataContext
        {
            CorrelationId = "corr-12345",
            CausationId = "caus-67890",
            UserId = "user-999",
            TenantId = "tenant-emea"
        };

        var provider = new KyrolusMartenDefaultEventMetadataProvider(() => context);
        var meta = provider.GetMetadata();

        meta["correlation-id"].ShouldBe("corr-12345");
        meta["causation-id"].ShouldBe("caus-67890");
        meta["user-id"].ShouldBe("user-999");
        meta["tenant-id"].ShouldBe("tenant-emea");
        meta.ContainsKey("timestamp-utc").ShouldBeTrue();
    }
}
