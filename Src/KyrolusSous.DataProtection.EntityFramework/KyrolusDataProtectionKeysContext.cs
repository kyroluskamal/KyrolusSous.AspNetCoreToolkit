using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.DataProtection.EntityFramework;

public sealed class KyrolusDataProtectionKeysContext : DbContext, IDataProtectionKeyContext
{
    public KyrolusDataProtectionKeysContext(DbContextOptions<KyrolusDataProtectionKeysContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
