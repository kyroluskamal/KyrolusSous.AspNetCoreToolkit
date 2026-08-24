namespace KyrolusSous.Mapping.UnitTests;

public sealed class NestedAndDeepMappingTests
{
    private sealed class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    private sealed class Customer
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public Address? Address { get; set; }
    }

    private sealed class Order
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public Customer? Customer { get; set; }
    }

    private sealed class AddressDto
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    private sealed class CustomerDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public AddressDto? Address { get; set; }
    }

    private sealed class OrderDto
    {
        public Guid OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public CustomerDto? Customer { get; set; }
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Recursively maps deep object hierarchies")]
    public void DeepMapping_RecursivelyMapsSubObjects()
    {
        var mapper = new KyrolusObjectMapper();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            TotalAmount = 500m,
            Customer = new Customer
            {
                Id = 1,
                FullName = "Alice",
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "Cairo"
                }
            }
        };

        var dto = mapper.Map<Order, OrderDto>(order);

        dto.ShouldNotBeNull();
        dto.OrderId.ShouldBe(order.OrderId);
        dto.TotalAmount.ShouldBe(500m);
        dto.Customer.ShouldNotBeNull();
        dto.Customer.Id.ShouldBe(1);
        dto.Customer.FullName.ShouldBe("Alice");
        dto.Customer.Address.ShouldNotBeNull();
        dto.Customer.Address.Street.ShouldBe("123 Main St");
        dto.Customer.Address.City.ShouldBe("Cairo");
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Null nested properties remain null in destination")]
    public void DeepMapping_NullNestedObject_RemainsNull()
    {
        var mapper = new KyrolusObjectMapper();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            TotalAmount = 250m,
            Customer = new Customer
            {
                Id = 2,
                FullName = "Bob",
                Address = null
            }
        };

        var dto = mapper.Map<Order, OrderDto>(order);

        dto.ShouldNotBeNull();
        dto.Customer.ShouldNotBeNull();
        dto.Customer.Address.ShouldBeNull();
    }
}
