using KyrolusSous.EndpointKit.Marten;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointKitMartenTests
{
    public sealed class OrderDoc
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
    }

    [Fact(DisplayName = "Marten FilterBuilder: Parses numeric and string filters properly")]
    public void Marten_FilterBuilder_Should_Parse_Expressions()
    {
        var expr = FilterBuilder.BuildFilterExpression<OrderDoc>("TotalAmount gte 100, CustomerName contains 'John'");
        expr.ShouldNotBeNull();

        var func = expr.Compile();
        func(new OrderDoc { TotalAmount = 150m, CustomerName = "John Doe" }).ShouldBeTrue();
        func(new OrderDoc { TotalAmount = 50m, CustomerName = "John Doe" }).ShouldBeFalse();
        func(new OrderDoc { TotalAmount = 150m, CustomerName = "Jane Smith" }).ShouldBeFalse();
    }

    [Fact(DisplayName = "Marten OrderBuilder: Builds ordering functions")]
    public void Marten_OrderBuilder_Should_Build_Ordering()
    {
        var orderFunc = OrderBuilder.BuildOrderBy<OrderDoc>("TotalAmount:desc", null, false, out var error);
        error.ShouldBeNull();
        orderFunc.ShouldNotBeNull();

        var docs = new List<OrderDoc>
        {
            new() { Id = Guid.NewGuid(), TotalAmount = 50m },
            new() { Id = Guid.NewGuid(), TotalAmount = 200m },
            new() { Id = Guid.NewGuid(), TotalAmount = 100m }
        }.AsQueryable();

        var sorted = orderFunc(docs).ToList();
        sorted[0].TotalAmount.ShouldBe(200m);
        sorted[1].TotalAmount.ShouldBe(100m);
        sorted[2].TotalAmount.ShouldBe(50m);
    }

    [Fact(DisplayName = "Marten ApiConfig: Configures routing and default settings")]
    public void Marten_ApiConfig_Should_Store_Configuration()
    {
        var config = new KyrolusMartenApiConfig<OrderDoc>
        {
            Route = "orders",
            ApiName = "OrdersDocAPI",
            DefaultPageSize = 50,
            MaxPageSize = 200
        };

        config.Route.ShouldBe("orders");
        config.ApiName.ShouldBe("OrdersDocAPI");
        config.DefaultPageSize.ShouldBe(50);
        config.MaxPageSize.ShouldBe(200);
    }
}
