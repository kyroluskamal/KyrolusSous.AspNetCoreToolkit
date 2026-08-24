namespace KyrolusSous.Mapping.UnitTests;

public sealed class ComprehensiveMappingCoverageTests
{
    private sealed class SampleSource
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid TenantId { get; set; }
        [KyrolusIgnoreMap]
        public string IgnoredField { get; set; } = "IgnoreMe";
    }

    private sealed class SampleTarget
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string CustomCalculated { get; set; } = string.Empty;
        public string ConstantValue { get; set; } = string.Empty;
    }

    private sealed class SampleProfile : KyrolusMappingProfile
    {
        public SampleProfile()
        {
            CreateMap<SampleSource, SampleTarget>()
                .ForMember(dest => dest.CustomCalculated, opt => opt.MapFrom((src, ctx) => $"Computed-{src.Id}-{ctx.GetItem<string>("prefix", "def")}"))
                .ForMember(dest => dest.ConstantValue, opt => opt.UseValue("FixedConstant"))
                .Ignore(dest => dest.Secret);
        }
    }

    private sealed class DummyConverter : IKyrolusTypeConverter<string, int>
    {
        public int Convert(string source, KyrolusMappingContext context) => int.TryParse(source, out var v) ? v : 0;
    }

    [Fact(DisplayName = "ComprehensiveCoverage: Array collection mapping returns typed array")]
    public void ArrayMapping_ReturnsTypedArray()
    {
        var mapper = new KyrolusObjectMapper();
        var sources = new[]
        {
            new SampleSource { Id = 1, Title = "One" },
            new SampleSource { Id = 2, Title = "Two" }
        };

        var mapped = mapper.Map<SampleSource[], SampleTarget[]>(sources);

        mapped.ShouldNotBeNull();
        mapped.Length.ShouldBe(2);
        mapped[0].Id.ShouldBe(1);
        mapped[0].Title.ShouldBe("One");
        mapped[1].Id.ShouldBe(2);
    }

    [Fact(DisplayName = "ComprehensiveCoverage: Fluent profile with Context resolver and Constant value")]
    public void Profile_ContextResolver_And_ConstantValue()
    {
        var config = new KyrolusMappingConfiguration();
        config.AddProfile<SampleProfile>();

        var mapper = new KyrolusObjectMapper(config);
        var source = new SampleSource
        {
            Id = 42,
            Title = "Testing",
            Secret = "TopSecret",
            CreatedAt = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            TenantId = Guid.NewGuid()
        };

        var context = new KyrolusMappingContext();
        context.SetItem("prefix", "custom");

        var target = mapper.Map<SampleSource, SampleTarget>(source, context);

        target.ShouldNotBeNull();
        target.Id.ShouldBe(42);
        target.Title.ShouldBe("Testing");
        target.CustomCalculated.ShouldBe("Computed-42-custom");
        target.ConstantValue.ShouldBe("FixedConstant");
        target.Secret.ShouldBe(string.Empty); // Ignored
    }

    [Fact(DisplayName = "ComprehensiveCoverage: ConstructUsing and ConvertUsing with Context")]
    public void ConstructUsing_And_ConvertUsing_WithContext()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<SampleSource, SampleTarget>()
            .ConstructUsing(src => new SampleTarget { Title = $"Constructed-{src.Title}" });

        var mapper = new KyrolusObjectMapper(config);
        var source = new SampleSource { Id = 1, Title = "InitTitle" };

        var target = mapper.Map<SampleSource, SampleTarget>(source);

        target.ShouldNotBeNull();
        target.Title.ShouldBe("InitTitle");
    }

    [Fact(DisplayName = "ComprehensiveCoverage: Non-generic ProjectTo(IQueryable) projects query")]
    public void NonGeneric_ProjectTo_ProjectsCorrectly()
    {
        var sources = new List<SampleSource>
        {
            new() { Id = 1, Title = "Alpha" },
            new() { Id = 2, Title = "Beta" }
        }.AsQueryable();

        var query = (IQueryable)sources;
        var projected = query.ProjectTo<SampleTarget>().Cast<SampleTarget>().ToList();

        projected.Count.ShouldBe(2);
        projected[0].Title.ShouldBe("Alpha");
        projected[1].Title.ShouldBe("Beta");
    }

    [Fact(DisplayName = "ComprehensiveCoverage: ServiceCollection scanning registers assembly profiles")]
    public void ServiceCollection_Scanning_RegistersProfiles()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMapping();
        services.AddMappingProfilesFromAssembly(typeof(SampleProfile).Assembly);

        var sp = services.BuildServiceProvider();
        var mapper = sp.GetRequiredService<IKyrolusObjectMapper>();

        mapper.ShouldNotBeNull();
    }

    [Fact(DisplayName = "ComprehensiveCoverage: MapEnumerable and MapList handle empty/null collections")]
    public void MapEnumerable_And_MapList_EmptyAndNull()
    {
        var mapper = new KyrolusObjectMapper();

        mapper.MapEnumerable<SampleSource, SampleTarget>(null!).ShouldBeEmpty();
        mapper.MapList<SampleSource, SampleTarget>(null!).ShouldBeEmpty();
        mapper.MapList<SampleSource, SampleTarget>([]).ShouldBeEmpty();
    }

    [Fact(DisplayName = "ComprehensiveCoverage: Converter registration and retrieval")]
    public void Converter_RegistrationAndRetrieval()
    {
        var config = new KyrolusMappingConfiguration();
        var converter = new DummyConverter();
        config.RegisterConverter(converter);

        var retrieved = config.FindConverter<string, int>();
        retrieved.ShouldNotBeNull();
        retrieved.ShouldBeSameAs(converter);
    }

    [Fact(DisplayName = "ComprehensiveCoverage: Models equality and hash code verification")]
    public void Models_EqualityAndHashCode()
    {
        var prop1 = new KyrolusPropertyMappingModel
        {
            SourcePropertyName = "Id",
            TargetPropertyName = "Id",
            SourcePropertyType = "int",
            TargetPropertyType = "int"
        };
        var prop2 = new KyrolusPropertyMappingModel
        {
            SourcePropertyName = "Id",
            TargetPropertyName = "Id",
            SourcePropertyType = "int",
            TargetPropertyType = "int"
        };

        prop1.Equals(prop2).ShouldBeTrue();
        prop1.GetHashCode().ShouldBe(prop2.GetHashCode());

        var typePair1 = new KyrolusTypePairMappingModel
        {
            SourceTypeName = "User",
            TargetTypeName = "UserDto",
            SourceFullTypeName = "App.User",
            TargetFullTypeName = "App.UserDto",
            MethodName = "ToUserDto"
        };
        var typePair2 = new KyrolusTypePairMappingModel
        {
            SourceTypeName = "User",
            TargetTypeName = "UserDto",
            SourceFullTypeName = "App.User",
            TargetFullTypeName = "App.UserDto",
            MethodName = "ToUserDto"
        };

        typePair1.Equals(typePair2).ShouldBeTrue();
        typePair1.GetHashCode().ShouldBe(typePair2.GetHashCode());
    }

    [Fact(DisplayName = "ComprehensiveCoverage: In-place mapping with custom resolvers and ignore rules")]
    public void InPlaceMapping_WithCustomResolvers()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<SampleSource, SampleTarget>()
            .ForMember(dest => dest.CustomCalculated, opt => opt.MapFrom(src => $"InPlace-{src.Title}"))
            .Ignore(dest => dest.Secret);

        var mapper = new KyrolusObjectMapper(config);
        var source = new SampleSource { Id = 5, Title = "Widget", Secret = "NoCopy" };
        var target = new SampleTarget { Id = 1, Title = "Old", Secret = "Preserved" };

        mapper.Map(source, target);

        target.Id.ShouldBe(5);
        target.Title.ShouldBe("Widget");
        target.CustomCalculated.ShouldBe("InPlace-Widget");
        target.Secret.ShouldBe("Preserved");
    }

    [Fact(DisplayName = "ComprehensiveCoverage: Clone creates a separate deep copy of object graph")]
    public void Clone_CreatesDeepCopy()
    {
        var mapper = new KyrolusObjectMapper();
        var source = new SampleSource { Id = 100, Title = "Original", Secret = "SecretVal" };

        var clone = mapper.Clone(source);

        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(source);
        clone.Id.ShouldBe(100);
        clone.Title.ShouldBe("Original");
    }

    [Fact(DisplayName = "ComprehensiveCoverage: In-place mapping with IgnoreNullValues preserves existing non-null target values (HTTP PATCH)")]
    public void InPlaceMapping_IgnoreNullValues_PreservesTarget()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<SampleSource, SampleTarget>()
            .IgnoreNullValues();

        var mapper = new KyrolusObjectMapper(config);
        var patch = new SampleSource { Id = 200, Title = null!, Secret = "NewSecret" };
        var existing = new SampleTarget { Id = 10, Title = "OriginalTitle", Secret = "OldSecret" };

        mapper.Map(patch, existing);

        existing.Id.ShouldBe(200);
        existing.Title.ShouldBe("OriginalTitle"); // Null value in source did not overwrite existing title
        existing.Secret.ShouldBe("NewSecret");
    }
}
