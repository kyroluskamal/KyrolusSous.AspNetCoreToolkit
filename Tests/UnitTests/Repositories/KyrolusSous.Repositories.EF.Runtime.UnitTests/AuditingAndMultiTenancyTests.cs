using KyrolusSous.Repositories.EF.Abstractions.Auditing;
using KyrolusSous.Repositories.EF.Abstractions.MultiTenancy;
using KyrolusSous.Repositories.EF.Runtime.Interceptors;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class AuditingAndMultiTenancyTests
{
    private sealed class Customer : IKyrolusAuditableEntity, IKyrolusTenantScopedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastModifiedAtUtc { get; set; }
        public string? LastModifiedBy { get; set; }
        public string TenantId { get; set; } = string.Empty;
    }

    private sealed class MockUserContext(string userId, string userName) : ICurrentUserContext
    {
        public string? UserId => userId;
        public string? UserName => userName;
        public bool IsAuthenticated => true;
    }

    private sealed class MockTenantContext(string tenantId) : ICurrentTenantContext
    {
        public string? TenantId => tenantId;
    }

    private sealed class AuditingDbContext : DbContext
    {
        public DbSet<Customer> Customers => Set<Customer>();

        public AuditingDbContext(DbContextOptions<AuditingDbContext> options) : base(options) { }
    }

    [Fact(DisplayName = "AuditingInterceptor: Populates CreatedAt and CreatedBy on insert")]
    public async Task AuditingInterceptor_SetsCreatedMetadata()
    {
        var userCtx = new MockUserContext("user-123", "kyrolus");
        var capturedAudits = new List<KyrolusAuditEntry>();

        var options = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new KyrolusAuditInterceptor(userCtx, entries => capturedAudits.AddRange(entries)))
            .Options;

        using var context = new AuditingDbContext(options);
        var customer = new Customer { Name = "Acme Corp" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        customer.CreatedAtUtc.ShouldNotBe(default);
        customer.CreatedBy.ShouldBe("user-123");
        capturedAudits.Count.ShouldBeGreaterThan(0);
        capturedAudits[0].Action.ShouldBe("Insert");
        capturedAudits[0].UserId.ShouldBe("user-123");
    }

    [Fact(DisplayName = "TenantInterceptor: Populates TenantId automatically on insert")]
    public async Task TenantInterceptor_SetsTenantId()
    {
        var tenantCtx = new MockTenantContext("tenant-alpha");

        var options = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new KyrolusTenantInterceptor(tenantCtx))
            .Options;

        using var context = new AuditingDbContext(options);
        var customer = new Customer { Name = "Tenant Customer" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        customer.TenantId.ShouldBe("tenant-alpha");
    }
}
