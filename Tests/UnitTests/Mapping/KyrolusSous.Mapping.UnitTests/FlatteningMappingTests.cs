namespace KyrolusSous.Mapping.UnitTests;

public sealed class FlatteningMappingTests
{
    private sealed class Address
    {
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }

    private sealed class Customer
    {
        public string Name { get; set; } = string.Empty;
        public Address? Address { get; set; }
    }

    private sealed class Order
    {
        public int Id { get; set; }
        public Customer? Customer { get; set; }
    }

    private sealed class OrderSummaryDto
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty; // Flattened from Customer.Name
        public string CustomerAddressCity { get; set; } = string.Empty; // Flattened from Customer.Address.City
        [KyrolusMapProperty("Customer.Address.ZipCode")]
        public string PostalCode { get; set; } = string.Empty; // Explicit path
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Automatically flattens nested properties into un-nested DTO properties")]
    public void Flattening_ResolvesNestedPaths()
    {
        var mapper = new KyrolusObjectMapper();
        var order = new Order
        {
            Id = 55,
            Customer = new Customer
            {
                Name = "Acme Corp",
                Address = new Address
                {
                    City = "Alexandria",
                    ZipCode = "21500"
                }
            }
        };

        var dto = mapper.Map<Order, OrderSummaryDto>(order);

        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(55);
        dto.CustomerName.ShouldBe("Acme Corp");
        dto.CustomerAddressCity.ShouldBe("Alexandria");
    }
}
