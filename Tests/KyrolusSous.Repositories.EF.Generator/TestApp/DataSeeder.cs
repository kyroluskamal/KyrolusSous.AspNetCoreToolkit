namespace KyrolusSous.Repositories.EF.Generator.TestApp;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        if (await db.Tenants.AnyAsync(ct))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var storeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var storeId2 = Guid.Parse("22222222-2222-2222-2222-222222222223");

        var roleAdminId = Guid.Parse("33333333-3333-3333-3333-333333333331");
        var roleManagerId = Guid.Parse("33333333-3333-3333-3333-333333333332");

        var userAliceId = Guid.Parse("44444444-4444-4444-4444-444444444441");
        var userBobId = Guid.Parse("44444444-4444-4444-4444-444444444442");

        var categoryElectronicsId = Guid.Parse("55555555-5555-5555-5555-555555555551");
        var categoryBooksId = Guid.Parse("55555555-5555-5555-5555-555555555552");

        var productLaptopId = Guid.Parse("66666666-6666-6666-6666-666666666661");
        var productHeadphonesId = Guid.Parse("66666666-6666-6666-6666-666666666662");
        var productBookId = Guid.Parse("66666666-6666-6666-6666-666666666663");

        var customerJohnId = Guid.Parse("77777777-7777-7777-7777-777777777771");
        var customerJaneId = Guid.Parse("77777777-7777-7777-7777-777777777772");

        var orderId = Guid.Parse("88888888-8888-8888-8888-888888888881");
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Contoso Shops",
            Domain = "contoso.test",
            Stores = []
        };

        var store1 = new Store
        {
            Id = storeId,
            TenantId = tenantId,
            Name = "Contoso US",
            Locale = "en-US",
            CreatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = now
        };

        var store2 = new Store
        {
            Id = storeId2,
            TenantId = tenantId,
            Name = "Contoso EU",
            Locale = "en-GB",
            CreatedAt = DateTimeOffset.Parse("2024-05-01T00:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = now
        };

        var roleAdmin = new Role { Id = roleAdminId, Name = "Admin" };
        var roleManager = new Role { Id = roleManagerId, Name = "Manager" };

        var userAlice = new User { Id = userAliceId, Name = "Alice", Email = "alice@contoso.test" };
        var userBob = new User { Id = userBobId, Name = "Bob", Email = "bob@contoso.test" };

        var storeUserRoles = new[]
        {
            new StoreUserRole { StoreId = storeId, UserId = userAliceId, RoleId = roleAdminId },
            new StoreUserRole { StoreId = storeId, UserId = userBobId, RoleId = roleManagerId }
        };

        var categoryElectronics = new Category
        {
            Id = categoryElectronicsId,
            StoreId = storeId,
            Name = "Electronics",
            Slug = "electronics",
            CreatedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = now
        };

        var categoryBooks = new Category
        {
            Id = categoryBooksId,
            StoreId = storeId,
            Name = "Books",
            Slug = "books",
            CreatedAt = now,
            UpdatedAt = now
        };

        var productLaptop = new Product
        {
            Id = productLaptopId,
            StoreId = storeId,
            Name = "Laptop Pro 15",
            Sku = "LP-15",
            Price = 1200m,
            StockQuantity = 25,
            IsActive = true,
            RowVersion = [0],
            CreatedAt = DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = now
        };

        var productHeadphones = new Product
        {
            Id = productHeadphonesId,
            StoreId = storeId,
            Name = "Noise Cancelling Headphones",
            Sku = "NC-100",
            Price = 199m,
            StockQuantity = 80,
            IsActive = true,
            RowVersion = [0],
            CreatedAt = DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = now
        };

        var productBook = new Product
        {
            Id = productBookId,
            StoreId = storeId,
            Name = "Clean Code",
            Sku = "BOOK-CC",
            Price = 35m,
            StockQuantity = 50,
            IsActive = true,
            RowVersion = [0],
            CreatedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = now
        };

        var productCategories = new[]
        {
            new ProductCategory { ProductId = productLaptopId, CategoryId = categoryElectronicsId },
            new ProductCategory { ProductId = productHeadphonesId, CategoryId = categoryElectronicsId },
            new ProductCategory { ProductId = productBookId, CategoryId = categoryBooksId }
        };

        var customerJohn = new Customer
        {
            Id = customerJohnId,
            TenantId = tenantId,
            StoreId = storeId,
            Name = "John Doe",
            Email = "john@customer.test",
            Phone = "+1000000001",
            Address = new Address
            {
                Street = "123 Main St",
                City = "Seattle",
                State = "WA",
                Country = "USA",
                ZipCode = "98101"
            },
            CreatedAt = now,
            UpdatedAt = now
        };

        var customerJane = new Customer
        {
            Id = customerJaneId,
            TenantId = tenantId,
            StoreId = storeId,
            Name = "Jane Smith",
            Email = "jane@customer.test",
            Phone = "+1000000002",
            Address = new Address
            {
                Street = "500 Pine St",
                City = "Seattle",
                State = "WA",
                Country = "USA",
                ZipCode = "98102"
            },
            CreatedAt = now,
            UpdatedAt = now
        };

        var order = new Order
        {
            Id = orderId,
            StoreId = storeId,
            CustomerId = customerJohnId,
            OrderNumber = "ORD-1001",
            Status = OrderStatus.Paid,
            Total = 1598m,
            RowVersion = new byte[] { 0 },
            CreatedAt = now,
            UpdatedAt = now
        };

        var orderLines = new[]
        {
            new OrderLine
            {
                OrderId = orderId,
                ProductId = productLaptopId,
                Quantity = 1,
                UnitPrice = 1200m,
                LineTotal = 1200m
            },
            new OrderLine
            {
                OrderId = orderId,
                ProductId = productHeadphonesId,
                Quantity = 2,
                UnitPrice = 199m,
                LineTotal = 398m
            }
        };

        var payment = new Payment
        {
            OrderId = orderId,
            Provider = "Stripe",
            ProviderRef = "pi_test_123",
            Amount = 1598m,
            Status = PaymentStatus.Paid,
            PaidAt = now
        };

        var review = new Review
        {
            ProductId = productLaptopId,
            CustomerId = customerJaneId,
            Rating = 5,
            Comment = "Great laptop, fast shipping.",
            CreatedAt = now
        };

        db.Tenants.Add(tenant);
        db.Stores.AddRange(store1, store2);
        db.Roles.AddRange(roleAdmin, roleManager);
        db.Users.AddRange(userAlice, userBob);
        db.StoreUserRoles.AddRange(storeUserRoles);
        db.Categories.AddRange(categoryElectronics, categoryBooks);
        db.Products.AddRange(productLaptop, productHeadphones, productBook);
        db.ProductCategories.AddRange(productCategories);
        db.Customers.AddRange(customerJohn, customerJane);
        db.Orders.Add(order);
        db.OrderLines.AddRange(orderLines);
        db.Payments.Add(payment);
        db.Reviews.Add(review);

        await db.SaveChangesAsync(ct);
    }
}
