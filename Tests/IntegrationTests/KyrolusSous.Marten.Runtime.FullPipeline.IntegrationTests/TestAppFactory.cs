using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KyrolusSous.Caching.Abstractions;
using Npgsql;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Marten"] = "Host=localhost;Port=5432;Database=kyrolus_marten_fullpipeline_tests;Username=postgres;Password=postgres;Maximum Pool Size=5;Minimum Pool Size=0;Connection Idle Lifetime=5;Command Timeout=30;Timeout=30",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Auth:SigningKey"] = "KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests.Auth.SigningKey.2026"
            };
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IKyrolusCacheProvider>();
            services.AddSingleton<IKyrolusCacheProvider, InMemoryIntegrationCacheProvider>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        NpgsqlConnection.ClearAllPools();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
    }
}
