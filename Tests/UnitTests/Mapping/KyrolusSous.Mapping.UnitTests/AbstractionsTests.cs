namespace KyrolusSous.Mapping.UnitTests;

public sealed class AbstractionsTests
{
    private sealed class SampleSource;
    private sealed class SampleTarget;

    [Fact(DisplayName = "KyrolusMapToAttribute: Initializes with TargetType and sets IsBidirectional via constructor and property")]
    public void MapToAttribute_Properties()
    {
        var attrNamed = new KyrolusMapToAttribute(typeof(SampleTarget)) { IsBidirectional = true };
        attrNamed.TargetType.ShouldBe(typeof(SampleTarget));
        attrNamed.IsBidirectional.ShouldBeTrue();

        var attrCtor = new KyrolusMapToAttribute(typeof(SampleTarget), isBidirectional: true);
        attrCtor.TargetType.ShouldBe(typeof(SampleTarget));
        attrCtor.IsBidirectional.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusMapFromAttribute: Initializes with SourceType and sets IsBidirectional via constructor and property")]
    public void MapFromAttribute_Properties()
    {
        var attrNamed = new KyrolusMapFromAttribute(typeof(SampleSource)) { IsBidirectional = true };
        attrNamed.SourceType.ShouldBe(typeof(SampleSource));
        attrNamed.IsBidirectional.ShouldBeTrue();

        var attrCtor = new KyrolusMapFromAttribute(typeof(SampleSource), isBidirectional: true);
        attrCtor.SourceType.ShouldBe(typeof(SampleSource));
        attrCtor.IsBidirectional.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusMapPropertyAttribute: Initializes SourceName and TargetName")]
    public void MapPropertyAttribute_Properties()
    {
        var attr = new KyrolusMapPropertyAttribute("OldName") { TargetName = "NewName" };
        attr.SourceName.ShouldBe("OldName");
        attr.TargetName.ShouldBe("NewName");
    }

    [Fact(DisplayName = "KyrolusUseConverterAttribute: Initializes ConverterType")]
    public void UseConverterAttribute_Properties()
    {
        var attr = new KyrolusUseConverterAttribute(typeof(SampleTarget));
        attr.ConverterType.ShouldBe(typeof(SampleTarget));
    }

    [Fact(DisplayName = "KyrolusMappingContext: Stores and retrieves custom items")]
    public void MappingContext_Items()
    {
        var context = new KyrolusMappingContext();
        context.SetItem("TenantId", "tenant-42");
        context.SetItem("UserId", 101);

        context.GetItem<string>("TenantId").ShouldBe("tenant-42");
        context.GetItem<int>("UserId").ShouldBe(101);
        context.GetItem<string>("NonExistent", "fallback").ShouldBe("fallback");

        context.Reset();
        context.GetItem<string>("TenantId").ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusMappingContext: Tracks circular object references by identity")]
    public void MappingContext_CircularTracking()
    {
        var context = new KyrolusMappingContext();
        var sourceObj = new SampleSource();
        var targetObj = new SampleTarget();

        context.TryGetMapped<SampleTarget>(sourceObj, out var found).ShouldBeFalse();
        found.ShouldBeNull();

        context.RegisterMapped(sourceObj, targetObj);

        context.TryGetMapped<SampleTarget>(sourceObj, out var existing).ShouldBeTrue();
        existing.ShouldBeSameAs(targetObj);

        context.Reset();
        context.TryGetMapped<SampleTarget>(sourceObj, out _).ShouldBeFalse();
    }
}
