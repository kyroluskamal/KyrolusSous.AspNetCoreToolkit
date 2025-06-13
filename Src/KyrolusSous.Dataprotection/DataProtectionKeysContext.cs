global using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace KyrolusSous.Dataprotection;
public class DataProtectionKeysContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeysContext(DbContextOptions<DataProtectionKeysContext> options)
                : base(options)
    {
        DataProtectionKeys = Set<DataProtectionKey>();
        if (Database.GetPendingMigrationsAsync().GetAwaiter().GetResult().Any())
        {
            // Database.MigrateAsync().GetAwaiter();
        }
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<DataProtectionKey>()
            .HasKey(x => x.Id);
        base.OnModelCreating(builder);
    }
}