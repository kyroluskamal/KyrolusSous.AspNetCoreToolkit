using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.OpenIddictAuth.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        // Initialization code
    }
}
