namespace KyrolusSous.Mapping.UnitTests;

public sealed class AdvancedFeaturesCoverageTests
{
    private sealed class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public bool IsVip { get; set; }
        public string? Note { get; set; }
    }

    private sealed class OrderDto
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public decimal VipDiscount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ProcessedBy { get; set; }
        public string? Note { get; set; }
    }

    private sealed class UnmappedTarget
    {
        public int Id { get; set; }
        public string MissingProperty { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "BeforeMap & AfterMap: Executes hooks properly during new instance mapping")]
    public void BeforeMap_AfterMap_NewInstance_ExecutesSuccessfully()
    {
        var beforeCalled = false;
        var afterCalled = false;

        var config = new KyrolusMappingConfiguration();
        config.CreateMap<Order, OrderDto>()
            .BeforeMap((src, dest) =>
            {
                beforeCalled = true;
                src.ShouldNotBeNull();
                dest.ShouldNotBeNull();
            })
            .AfterMap((src, dest, ctx) =>
            {
                afterCalled = true;
                dest.Status = "Completed";
                dest.ProcessedBy = ctx.GetItem<string>("Operator", "System");
            });

        var mapper = new KyrolusObjectMapper(config);
        var order = new Order { Id = 100, Total = 500 };
        var ctx = new KyrolusMappingContext();
        ctx.SetItem("Operator", "AdminUser");

        var dto = mapper.Map<Order, OrderDto>(order, ctx);

        beforeCalled.ShouldBeTrue();
        afterCalled.ShouldBeTrue();
        dto.Id.ShouldBe(100);
        dto.Total.ShouldBe(500);
        dto.Status.ShouldBe("Completed");
        dto.ProcessedBy.ShouldBe("AdminUser");
    }

    [Fact(DisplayName = "BeforeMap & AfterMap: Executes hooks properly during in-place mapping")]
    public void BeforeMap_AfterMap_InPlace_ExecutesSuccessfully()
    {
        var beforeTriggered = false;
        var afterTriggered = false;

        var config = new KyrolusMappingConfiguration();
        config.CreateMap<Order, OrderDto>()
            .BeforeMap((src, dest, ctx) =>
            {
                beforeTriggered = true;
            })
            .AfterMap((src, dest) =>
            {
                afterTriggered = true;
                dest.Status = "InPlaceUpdated";
            });

        var mapper = new KyrolusObjectMapper(config);
        var order = new Order { Id = 200, Total = 750 };
        var dto = new OrderDto();

        mapper.Map(order, dto);

        beforeTriggered.ShouldBeTrue();
        afterTriggered.ShouldBeTrue();
        dto.Id.ShouldBe(200);
        dto.Total.ShouldBe(750);
        dto.Status.ShouldBe("InPlaceUpdated");
    }

    [Fact(DisplayName = "Condition: Copies property only when predicate evaluates to true")]
    public void Condition_CopiesOnlyWhenPredicateIsTrue()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<Order, OrderDto>()
            .ForMember(dest => dest.VipDiscount, opt =>
            {
                opt.MapFrom(src => src.Total * 0.10m);
                opt.Condition(src => src.IsVip);
            })
            .ForMember(dest => dest.Note, opt =>
            {
                opt.Condition((src, ctx) => ctx.GetItem<bool>("AllowNotes", true));
            });

        var mapper = new KyrolusObjectMapper(config);

        // Case 1: IsVip = false -> VipDiscount should remain default (0)
        var normalOrder = new Order { Id = 1, Total = 100, IsVip = false, Note = "Normal" };
        var normalDto = mapper.Map<Order, OrderDto>(normalOrder);
        normalDto.VipDiscount.ShouldBe(0);
        normalDto.Note.ShouldBe("Normal");

        // Case 2: IsVip = true -> VipDiscount should be mapped (10)
        var vipOrder = new Order { Id = 2, Total = 100, IsVip = true, Note = "VIP note" };
        var vipDto = mapper.Map<Order, OrderDto>(vipOrder);
        vipDto.VipDiscount.ShouldBe(10);
        vipDto.Note.ShouldBe("VIP note");

        // Case 3: Condition with Context evaluating to false
        var ctx = new KyrolusMappingContext();
        ctx.SetItem("AllowNotes", false);
        var ctxDto = mapper.Map<Order, OrderDto>(vipOrder, ctx);
        ctxDto.Note.ShouldBeNull(); // Note conditioned to false via context
    }

    [Fact(DisplayName = "AssertConfigurationIsValid: Throws KyrolusMappingValidationException when unmapped properties exist")]
    public void AssertConfigurationIsValid_ThrowsWhenUnmappedPropertiesExist()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<Order, UnmappedTarget>();

        var ex = Should.Throw<KyrolusMappingValidationException>(() => config.AssertConfigurationIsValid());
        ex.Message.ShouldContain("MissingProperty");
        ex.Message.ShouldContain("Order");
        ex.Message.ShouldContain("UnmappedTarget");
    }

    [Fact(DisplayName = "AssertConfigurationIsValid: Passes when all properties are mapped or ignored")]
    public void AssertConfigurationIsValid_PassesWhenValid()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<Order, UnmappedTarget>()
            .Ignore(dest => dest.MissingProperty);

        // Should not throw
        Should.NotThrow(() => config.AssertConfigurationIsValid());
    }
}
