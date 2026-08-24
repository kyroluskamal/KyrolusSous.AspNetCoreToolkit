using System.Collections;
using System.Collections.Generic;
using KyrolusSous.Mapping.Generator.Diagnostics;
using KyrolusSous.Mapping.Runtime.Engine;

namespace KyrolusSous.Mapping.UnitTests;

public sealed class FullCoverageEdgeCasesTests
{
    private sealed class SampleSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Extra { get; set; } = string.Empty;
    }

    private sealed class SampleTarget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Extra { get; set; } = string.Empty;
        public string Computed { get; set; } = string.Empty;
    }

    private sealed class NestedBranch
    {
        public string Leaf { get; set; } = string.Empty;
    }

    private sealed class NestedRoot
    {
        public NestedBranch? Branch { get; set; }
        public string BranchOther { get; set; } = string.Empty;
    }

    private sealed class CustomEnumerable : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)new[] { 1, 2, 3 }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class DiagnosticProfile : KyrolusMappingProfile
    {
        public DiagnosticProfile()
        {
            CreateMap<SampleSource, SampleTarget>()
                .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name.ToUpperInvariant()))
                .Ignore(d => d.Extra)
                .BeforeMap((s, d) => { })
                .BeforeMap((s, d, c) => { })
                .AfterMap((s, d) => { })
                .AfterMap((s, d, c) => { })
                .IgnoreNullValues()
                .ReverseMap();
        }
    }

    [Fact(DisplayName = "Diagnostics: KyrolusMappingDiagnostics descriptors are defined")]
    public void Diagnostics_Descriptors_AreValid()
    {
        KyrolusMappingDiagnostics.UnmappedProperty.ShouldNotBeNull();
        KyrolusMappingDiagnostics.UnmappedProperty.Id.ShouldBe("KYMAP001");
        KyrolusMappingDiagnostics.IncompatiblePropertyType.ShouldNotBeNull();
        KyrolusMappingDiagnostics.IncompatiblePropertyType.Id.ShouldBe("KYMAP002");
    }

    [Fact(DisplayName = "KyrolusTypeMappingRule: MergeInto copies all fields and throws on null")]
    public void TypeMappingRule_MergeInto_CopiesAllConfiguration()
    {
        var rule1 = new KyrolusTypeMappingRule(typeof(SampleSource), typeof(SampleTarget));
        rule1.IgnoredMembers.Add("Extra");
        rule1.CustomMemberResolvers["Name"] = (s, c) => "Resolved";
        rule1.MemberConditions["Id"] = (s, c) => true;
        rule1.PropertyNameMappings["Name"] = "Name";
        rule1.BeforeMapActions.Add((s, d, c) => { });
        rule1.AfterMapActions.Add((s, d, c) => { });
        rule1.CustomTypeConverter = (s, c) => new SampleTarget();
        rule1.CustomConstructor = (Func<SampleSource, SampleTarget>)(s => new SampleTarget());
        rule1.IgnoreNullValues = true;
        rule1.AllowNullDestinationValues = false;

        var rule2 = new KyrolusTypeMappingRule(typeof(SampleSource), typeof(SampleTarget));
        rule1.MergeInto(rule2);

        rule2.IgnoredMembers.ShouldContain("Extra");
        rule2.CustomMemberResolvers.ContainsKey("Name").ShouldBeTrue();
        rule2.MemberConditions.ContainsKey("Id").ShouldBeTrue();
        rule2.PropertyNameMappings.ContainsKey("Name").ShouldBeTrue();
        rule2.BeforeMapActions.Count.ShouldBe(1);
        rule2.AfterMapActions.Count.ShouldBe(1);
        rule2.CustomTypeConverter.ShouldNotBeNull();
        rule2.CustomConstructor.ShouldNotBeNull();
        rule2.IgnoreNullValues.ShouldBeTrue();
        rule2.AllowNullDestinationValues.ShouldBeFalse();

        Should.Throw<ArgumentNullException>(() => rule1.MergeInto(null!));
        Should.Throw<ArgumentNullException>(() => new KyrolusTypeMappingRule(null!, typeof(SampleTarget)));
        Should.Throw<ArgumentNullException>(() => new KyrolusTypeMappingRule(typeof(SampleSource), null!));
    }

    [Fact(DisplayName = "KyrolusMappingContext: Edge cases with nulls, equals, and reset")]
    public void MappingContext_EdgeCases()
    {
        var context = new KyrolusMappingContext();

        // Null checks
        context.TryGetMapped(null!, typeof(SampleTarget), out var target1).ShouldBeFalse();
        target1.ShouldBeNull();
        context.TryGetMapped(new object(), null!, out var target2).ShouldBeFalse();
        target2.ShouldBeNull();
        context.TryGetMapped<SampleTarget>(null!, out var typedTarget).ShouldBeFalse();
        typedTarget.ShouldBeNull();

        // Register nulls safely
        context.RegisterMapped(null!, new object());
        context.RegisterMapped(new object(), null!);

        // Items manipulation and reset
        context.SetItem("Key1", "Val1");
        context.GetItem<string>("Key1").ShouldBe("Val1");
        context.GetItem<string>("NonExistent", "Default").ShouldBe("Default");
        context.GetItem<int>("NonExistentInt", 99).ShouldBe(99);

        // Reset clears reference map and items
        context.Reset();
        context.GetItem<string>("Key1").ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Null arguments return appropriate defaults")]
    public void ObjectMapper_NullSafety()
    {
        var mapper = new KyrolusObjectMapper();

        // Null source mappings
        mapper.Map<SampleSource, SampleTarget>(null!).ShouldBeNull();
        mapper.Map<SampleTarget>((object)null!).ShouldBeNull();
        mapper.Clone<SampleSource>(null!).ShouldBeNull();

        // Null collections
        mapper.MapEnumerable<SampleSource, SampleTarget>(null!).ShouldBeEmpty();
        mapper.MapList<SampleSource, SampleTarget>(null!).ShouldBeEmpty();

        // Null in-place
        var existing = new SampleTarget { Id = 5 };
        mapper.Map<SampleSource, SampleTarget>(null!, existing).ShouldBeSameAs(existing);
        mapper.Map<SampleSource, SampleTarget>(new SampleSource(), (SampleTarget)null!).ShouldBeNull();

        // GetProjection
        var proj = mapper.GetProjection<SampleSource, SampleTarget>();
        proj.ShouldNotBeNull();

        // Null context throw
        KyrolusMappingContext nullContext = null!;
        Should.Throw<ArgumentNullException>(() => mapper.Map<SampleSource, SampleTarget>(source: new SampleSource(), context: nullContext));
        Should.Throw<ArgumentNullException>(() => mapper.Map<SampleTarget>(source: (object)new SampleSource(), context: nullContext));
        Should.Throw<ArgumentNullException>(() => mapper.Map(source: new SampleSource(), target: existing, context: nullContext));
        Should.Throw<ArgumentNullException>(() => mapper.Clone(source: new SampleSource(), context: nullContext));
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: Scanning and registering profiles from Assembly")]
    public void ServiceCollection_Scanning_Works()
    {
        var services = new ServiceCollection();
        services.AddMappingProfile<DiagnosticProfile>();
        services.AddMappingProfilesFromAssembly(typeof(DiagnosticProfile).Assembly);
        services.AddKyrolusMapping();

        var sp = services.BuildServiceProvider();
        var mapper = sp.GetRequiredService<IKyrolusObjectMapper>();
        mapper.ShouldNotBeNull();

        var src = new SampleSource { Id = 1, Name = "test", Extra = "Secret" };
        var res = mapper.Map<SampleSource, SampleTarget>(src);
        res.Name.ShouldBe("TEST");
        res.Extra.ShouldBe(string.Empty); // Ignored in DiagnosticProfile
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: Null arguments throw ArgumentNullException")]
    public void ServiceCollection_NullChecks()
    {
        IServiceCollection nullServices = null!;
        Should.Throw<ArgumentNullException>(() => nullServices.AddKyrolusMapping());
        Should.Throw<ArgumentNullException>(() => nullServices.AddMappingProfile<DiagnosticProfile>());
        Should.Throw<ArgumentNullException>(() => nullServices.AddMappingProfilesFromAssembly(typeof(DiagnosticProfile).Assembly));

        var validServices = new ServiceCollection();
        Should.Throw<ArgumentNullException>(() => validServices.AddMappingProfilesFromAssembly(null!));
    }

    [Fact(DisplayName = "KyrolusMemberFlatteningResolver: Backtracking, null evaluation, and exact path")]
    public void MemberFlatteningResolver_EdgeCases()
    {
        // Path with null branch evaluation
        var path = KyrolusMemberFlatteningResolver.ResolveFlattenedPath(typeof(NestedRoot), "BranchLeaf");
        path.ShouldNotBeNull();
        path.Length.ShouldBe(2);

        var rootWithNull = new NestedRoot { Branch = null };
        var val = KyrolusMemberFlatteningResolver.EvaluatePath(path, rootWithNull);
        val.ShouldBeNull();

        var rootWithVal = new NestedRoot { Branch = new NestedBranch { Leaf = "Green" } };
        var val2 = KyrolusMemberFlatteningResolver.EvaluatePath(path, rootWithVal);
        val2.ShouldBe("Green");

        // Backtracking test: prefix matches "Branch" but property "BranchOther" is direct match on root
        var directPath = KyrolusMemberFlatteningResolver.ResolveFlattenedPath(typeof(NestedRoot), "BranchOther");
        directPath.ShouldNotBeNull();
        directPath.Length.ShouldBe(1);

        // Non-existent path
        var nonPath = KyrolusMemberFlatteningResolver.ResolveFlattenedPath(typeof(NestedRoot), "NonExistentPath");
        nonPath.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusCollectionMappingHelper: Dictionary and custom enumerable types")]
    public void CollectionMappingHelper_EdgeCases()
    {
        // Dictionary should not be treated as simple collection
        KyrolusCollectionMappingHelper.IsCollectionType(typeof(Dictionary<string, int>), out var dictElem).ShouldBeFalse();
        dictElem.ShouldBeNull();

        // Custom class implementing IEnumerable<int>
        KyrolusCollectionMappingHelper.IsCollectionType(typeof(CustomEnumerable), out var customElem).ShouldBeTrue();
        customElem.ShouldBe(typeof(int));
    }

    [Fact(DisplayName = "KyrolusTypeMappingExpression: Unary expressions, convert with context, and exceptions")]
    public void TypeMappingExpression_EdgeCases()
    {
        var rule = new KyrolusTypeMappingRule(typeof(SampleSource), typeof(SampleTarget));
        var expr = new KyrolusTypeMappingExpression<SampleSource, SampleTarget>(rule);

        // ConvertUsing with context
        expr.ConvertUsing((src, ctx) => new SampleTarget { Name = "FromContextConverter" });
        rule.CustomTypeConverter.ShouldNotBeNull();

        // ConstructUsing
        expr.ConstructUsing(src => new SampleTarget { Name = "Constructed" });
        rule.CustomConstructor.ShouldNotBeNull();

        // Unary expression unwrapping (boxing conversion e.g. dest => (object)dest.Id)
        expr.ForMember(d => (object)d.Id, opt =>
        {
            opt.UseValue(42);
            opt.MapFrom((src, ctx) => 42);
            opt.Ignore();
        });

        // Invalid member expression (not a property access) throws ArgumentException
        Should.Throw<ArgumentException>(() => expr.ForMember(d => 100, opt => { }));

        // ReverseMap when no factory provided throws InvalidOperationException
        Should.Throw<InvalidOperationException>(() => expr.ReverseMap());
    }

    private enum TestStatus
    {
        Pending = 1,
        Approved = 2
    }

    private sealed class TypeConversionSource
    {
        public string StatusStr { get; set; } = "Approved";
        public int StatusInt { get; set; } = 2;
        public TestStatus StatusEnum { get; set; } = TestStatus.Approved;
        public string GuidStr { get; set; } = "d3b07384-d113-4632-a5e2-63b7dfa9fd63";
        public Guid GuidVal { get; set; } = Guid.Parse("d3b07384-d113-4632-a5e2-63b7dfa9fd63");
        public DateTime DtMin { get; set; } = DateTime.MinValue;
        public DateTime DtMax { get; set; } = DateTime.MaxValue;
        public DateTime DtCustom { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public DateTimeOffset DtoVal { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public NestedRoot Nested { get; set; } = new NestedRoot { Branch = new NestedBranch { Leaf = "LeafVal" } };
    }

    private sealed class TypeConversionTarget
    {
        public TestStatus StatusStr { get; set; }
        public TestStatus StatusInt { get; set; }
        public string StatusEnum { get; set; } = string.Empty;
        public Guid GuidStr { get; set; }
        public string GuidVal { get; set; } = string.Empty;
        public DateTimeOffset DtMin { get; set; }
        public DateTimeOffset DtMax { get; set; }
        public DateTimeOffset DtCustom { get; set; }
        public DateTime DtoVal { get; set; }
        public string NestedBranchLeaf { get; set; } = string.Empty;
    }

    private sealed class ComplexCtorTarget(int id, string customName, string flattenedLeaf = "DefaultLeaf", int optional = 42)
    {
        public int Id { get; } = id;
        public string CustomName { get; } = customName;
        public string FlattenedLeaf { get; } = flattenedLeaf;
        public int Optional { get; } = optional;
    }

    [Fact(DisplayName = "KyrolusExpressionMappingEngine: Comprehensive type conversions and constructor bindings")]
    public void Engine_TypeConversions_And_ConstructorBindings()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<TypeConversionSource, ComplexCtorTarget>()
            .ForMember(d => d.CustomName, opt => opt.MapFrom(s => "CustomResolved"));

        var mapper = new KyrolusObjectMapper(config);

        // 1. Type conversions
        var src = new TypeConversionSource();
        var target = mapper.Map<TypeConversionSource, TypeConversionTarget>(src);

        target.StatusStr.ShouldBe(TestStatus.Approved);
        target.StatusInt.ShouldBe(TestStatus.Approved);
        target.StatusEnum.ShouldBe("Approved");
        target.GuidStr.ShouldBe(Guid.Parse("d3b07384-d113-4632-a5e2-63b7dfa9fd63"));
        target.GuidVal.ShouldBe("d3b07384-d113-4632-a5e2-63b7dfa9fd63");
        target.DtMin.ShouldBe(DateTimeOffset.MinValue);
        target.DtMax.ShouldBe(DateTimeOffset.MaxValue);
        target.DtCustom.Year.ShouldBe(2026);
        target.DtoVal.Year.ShouldBe(2026);

        // In-place with flattening
        var inPlaceTarget = new TypeConversionTarget();
        mapper.Map(src, inPlaceTarget);
        inPlaceTarget.StatusStr.ShouldBe(TestStatus.Approved);

        // 2. Complex constructor bindings with custom resolver and default params
        var ctorTarget = mapper.Map<TypeConversionSource, ComplexCtorTarget>(src);
        ctorTarget.CustomName.ShouldBe("CustomResolved");
        ctorTarget.Optional.ShouldBe(42);
    }
}
