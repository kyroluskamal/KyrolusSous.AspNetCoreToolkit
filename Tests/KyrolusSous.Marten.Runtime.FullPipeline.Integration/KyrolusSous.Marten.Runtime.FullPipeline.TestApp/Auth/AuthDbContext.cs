using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Auth;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}
