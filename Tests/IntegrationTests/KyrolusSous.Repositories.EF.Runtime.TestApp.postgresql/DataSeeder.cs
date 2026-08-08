
namespace KyrolusSous.Repositories.EF.Runtime.TestApp;

public static class DataSeeder
{

    public readonly static Guid tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public readonly static Guid storeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public readonly static Guid storeId2 = Guid.Parse("22222222-2222-2222-2222-222222222223");

    public readonly static Guid roleAdminId = Guid.Parse("33333333-3333-3333-3333-333333333331");
    public readonly static Guid roleManagerId = Guid.Parse("33333333-3333-3333-3333-333333333332");

    public readonly static Guid userAliceId = Guid.Parse("44444444-4444-4444-4444-444444444441");
    public readonly static Guid userBobId = Guid.Parse("44444444-4444-4444-4444-444444444442");

    public readonly static Guid categoryElectronicsId = Guid.Parse("55555555-5555-5555-5555-555555555551");
    public readonly static Guid categoryBooksId = Guid.Parse("55555555-5555-5555-5555-555555555552");

    public readonly static Guid productLaptopId = Guid.Parse("66666666-6666-6666-6666-666666666661");
    public readonly static Guid productHeadphonesId = Guid.Parse("66666666-6666-6666-6666-666666666662");
    public readonly static Guid productBookId = Guid.Parse("66666666-6666-6666-6666-666666666663");

    public readonly static Guid customerJohnId = Guid.Parse("77777777-7777-7777-7777-777777777771");
    public readonly static Guid customerJaneId = Guid.Parse("77777777-7777-7777-7777-777777777772");

    public readonly static Guid orderId = Guid.Parse("88888888-8888-8888-8888-888888888881");
    public static readonly object[] ReviewLapTopKey = [productLaptopId, customerJaneId];

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        if (await db.Tenants.AnyAsync(ct)) return;

        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant()
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
            CreatedAt = DateTimeOffset.Parse("2025-01-01T00:05:00Z", CultureInfo.InvariantCulture),
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
            AddedAt = new TimeOnly(10, 30),
            AddedIn = new DateOnly(2024, 6, 15),
            FinishedAt = TimeSpan.FromDays(1),
            IsActive = true,
            Weight = null,
            Count = 10,
            RowVersion = [0],
            DiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture),
        };

        var productHeadphones = new Product
        {
            Id = productHeadphonesId,
            StoreId = storeId,
            Name = "Noise Cancelling Headphones",
            Sku = "NC-100",
            Price = 199m,
            StockQuantity = 80,
            AddedAt = new TimeOnly(14, 0),
            AddedIn = new DateOnly(2024, 8, 5),
            UpdatedAt = DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture),
            FinishedAt = TimeSpan.FromDays(2),
            IsActive = true,
            RowVersion = [0],
            Weight = 0.25m,
            Count = 5,
            DiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture),
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
            AddedIn = new DateOnly(2025, 1, 1),
            AddedAt = new TimeOnly(9, 0),
            UpdatedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            FinishedAt = TimeSpan.FromDays(1),
            RowVersion = [0],
            Weight = 0.5m,
            Count = null,
            DiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
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

        var reviewLaptop = new Review
        {
            ProductId = productLaptopId,
            CustomerId = customerJaneId,
            Rating = 5,
            Comment = "Great laptop, fast shipping.",
            CreatedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            AddedIn = new DateOnly(2024, 6, 15),
            AddedAt = new TimeOnly(10, 30),
            FinishedAt = TimeSpan.FromDays(1),
            DiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var reviewHeadphones = new Review
        {
            ProductId = productHeadphonesId,
            CustomerId = customerJohnId,
            Rating = 3,
            Comment = "Good sound, a bit tight.",
            CreatedAt = DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture),
            AddedIn = new DateOnly(2024, 8, 5),
            AddedAt = new TimeOnly(14, 0),
            FinishedAt = TimeSpan.FromDays(2),
            DiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var reviewBook = new Review
        {
            ProductId = productBookId,
            CustomerId = customerJaneId,
            Rating = 4,
            Comment = "Solid read, clear concepts.",
            CreatedAt = DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture),
            AddedIn = new DateOnly(2025, 1, 1),
            AddedAt = new TimeOnly(9, 0),
            FinishedAt = TimeSpan.FromDays(1),
            DiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
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
        db.Reviews.AddRange(reviewLaptop, reviewHeadphones, reviewBook);

        await db.SaveChangesAsync(ct);
    }
}

