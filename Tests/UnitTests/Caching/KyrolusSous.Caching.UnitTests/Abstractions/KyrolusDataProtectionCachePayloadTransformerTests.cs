using System.Text;
using KyrolusSous.Caching.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusDataProtectionCachePayloadTransformerTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public KyrolusDataProtectionCachePayloadTransformerTests()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        _dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();
    }

    [Fact(DisplayName = "Data Protection Cache Payload Transformer Protects And Unprotects Successfully")]
    public void DataProtection_Roundtrip_Success()
    {
        var transformer = new KyrolusDataProtectionCachePayloadTransformer(_dataProtectionProvider);
        var original = Encoding.UTF8.GetBytes("Super secret payload for user caching");

        var encrypted = transformer.Transform(original);
        encrypted.ShouldNotBeNull();
        encrypted.Length.ShouldBeGreaterThan(original.Length);
        encrypted.ShouldNotBe(original);

        var decrypted = transformer.Restore(encrypted);
        decrypted.ShouldBe(original);
    }

    [Fact(DisplayName = "Data Protection Cache Payload Transformer Handles Empty Payload")]
    public void DataProtection_HandlesEmptyPayload()
    {
        var transformer = new KyrolusDataProtectionCachePayloadTransformer(_dataProtectionProvider);
        var empty = Array.Empty<byte>();

        var encrypted = transformer.Transform(empty);
        encrypted.ShouldBeEmpty();

        var decrypted = transformer.Restore(empty);
        decrypted.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Data Protection Cache Payload Transformer Throws On Null Payload")]
    public void DataProtection_ThrowsOnNullPayload()
    {
        var transformer = new KyrolusDataProtectionCachePayloadTransformer(_dataProtectionProvider);

        Should.Throw<ArgumentNullException>(() => transformer.Transform(null!));
        Should.Throw<ArgumentNullException>(() => transformer.Restore(null!));
    }
}
