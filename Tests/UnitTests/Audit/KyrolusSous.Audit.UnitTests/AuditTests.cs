using KyrolusSous.Audit.Abstractions;
using KyrolusSous.Audit.Core;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Audit.UnitTests;

[KyrolusAuditable]
public class AuditedCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [KyrolusAuditIgnore]
    public string InternalSecret { get; set; } = string.Empty;
}

public class TestAuditDbContext(DbContextOptions<TestAuditDbContext> options, KyrolusAuditDbContextInterceptor interceptor) : DbContext(options)
{
    private readonly KyrolusAuditDbContextInterceptor _interceptor = interceptor;

    public DbSet<AuditedCustomer> Customers => Set<AuditedCustomer>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_interceptor);
        base.OnConfiguring(optionsBuilder);
    }
}

public sealed class AuditTests
{
    [Fact(DisplayName = "Audit DbContext Interceptor Tracks Insert And Update Correctly")]
    public async Task AuditInterceptor_TracksInsertAndUpdate_Correctly()
    {
        var auditStore = new KyrolusInMemoryAuditStore();
        var interceptor = new KyrolusAuditDbContextInterceptor(auditStore);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using (var db = new TestAuditDbContext(options, interceptor))
        {
            var customer = new AuditedCustomer
            {
                Name = "Kyrolus",
                Email = "kyrolus@example.com",
                InternalSecret = "Hidden"
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }

        var historyAfterInsert = await auditStore.GetEntityHistoryAsync("AuditedCustomer", "1");
        historyAfterInsert.Count.ShouldBe(1);
        historyAfterInsert[0].Action.ShouldBe(KyrolusAuditAction.Create);
        historyAfterInsert[0].Changes.Any(c => c.PropertyName == "Name" && (string?)c.NewValue == "Kyrolus").ShouldBeTrue();
        historyAfterInsert[0].Changes.Any(c => c.PropertyName == "InternalSecret").ShouldBeFalse(); // Ignored

        using (var db = new TestAuditDbContext(options, interceptor))
        {
            var customer = await db.Customers.FirstAsync();
            customer.Name = "Kyrolus Kamal";
            await db.SaveChangesAsync();
        }

        var historyAfterUpdate = await auditStore.GetEntityHistoryAsync("AuditedCustomer", "1");
        historyAfterUpdate.Count.ShouldBe(2);
        historyAfterUpdate[0].Action.ShouldBe(KyrolusAuditAction.Update);
        var nameChange = historyAfterUpdate[0].Changes.First(c => c.PropertyName == "Name");
        nameChange.OriginalValue.ShouldBe("Kyrolus");
        nameChange.NewValue.ShouldBe("Kyrolus Kamal");
    }
}
