using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
                ["ConnectionStrings:Marten"] = "Host=localhost;Port=5432;Database=kyrolus_marten_fullpipeline_tests;Username=postgres;Password=postgres",
                ["ConnectionStrings:OpenIddict"] = "Host=localhost;Port=5432;Database=kyrolus_marten_fullpipeline_tests;Username=postgres;Password=postgres",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["OpenIddict:UseEphemeralKeys"] = "true"
            };
            config.AddInMemoryCollection(overrides);
        });
    }
}
