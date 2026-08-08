namespace KyrolusSous.Repositories.EF.Generator.TestApp;

public class ApplicationDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<StoreUserRole> StoreUserRoles { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Review> Reviews { get; set; }
#pragma warning disable S3776
    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

            foreach (var entityType in mb.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (clrType is null)
                {
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        mb.Entity(clrType).Property(property.Name).HasConversion(dateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        mb.Entity(clrType).Property(property.Name).HasConversion(nullableDateTimeOffsetConverter);
                    }
                }
            }
        }
#pragma warning restore S3776
        // Primary keys
        // Keys
        mb.Entity<ProductCategory>().HasKey(x => new { x.ProductId, x.CategoryId });
        mb.Entity<StoreUserRole>().HasKey(x => new { x.StoreId, x.UserId, x.RoleId });
        mb.Entity<Payment>().HasKey(x => x.OrderId);
        mb.Entity<OrderLine>().HasKey(x => new { x.OrderId, x.ProductId });
        mb.Entity<Review>().HasKey(x => new { x.ProductId, x.CustomerId });

        // Indexes / unique constraints
        mb.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        mb.Entity<Product>().HasIndex(x => new { x.StoreId, x.Sku }).IsUnique();
        mb.Entity<Order>().HasIndex(x => new { x.StoreId, x.OrderNumber }).IsUnique();
        mb.Entity<Category>().HasIndex(x => new { x.StoreId, x.Slug }).IsUnique();
        mb.Entity<Review>().HasIndex(x => new { x.ProductId, x.CustomerId }).IsUnique();

        // Owned types
        mb.Entity<Customer>().OwnsOne(x => x.Address);

        // Concurrency tokens
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            mb.Entity<Product>().Property(x => x.RowVersion)
                .IsConcurrencyToken()
                .IsRequired(false)
                .ValueGeneratedNever();
            mb.Entity<Order>().Property(x => x.RowVersion)
                .IsConcurrencyToken()
                .IsRequired(false)
                .ValueGeneratedNever();
        }
        else
        {
            mb.Entity<Product>().Property(x => x.RowVersion).IsRowVersion();
            mb.Entity<Order>().Property(x => x.RowVersion).IsRowVersion();
        }

        // Soft delete filters
        // mb.Entity<Customer>().HasQueryFilter(x => !x.IsDeleted);
        // mb.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);
        // mb.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);

        // Relationships
        mb.Entity<Order>()
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId);
    }

}
