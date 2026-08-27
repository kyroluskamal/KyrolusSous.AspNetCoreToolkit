using System.Security.Claims;
using KyrolusSous.Auth.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.MultiTenancy.UnitTests;

public class MultiTenancyTests
{
    [Fact]
    public async Task HeaderResolver_ExtractsTenantId()
    {
        var resolver = new KyrolusHeaderTenantResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-apple";

        var tenantId = await resolver.ResolveTenantIdAsync(context);

        tenantId.ShouldBe("tenant-apple");
    }

    [Fact]
    public async Task ClaimResolver_ExtractsTenantClaim()
    {
        var resolver = new KyrolusClaimTenantResolver();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tenant_id", "tenant-microsoft")
            ], "TestAuth"))
        };

        var tenantId = await resolver.ResolveTenantIdAsync(context);

        tenantId.ShouldBe("tenant-microsoft");
    }

    [Fact]
    public async Task ClaimResolver_RejectsNonAsciiTenantClaim()
    {
        var resolver = new KyrolusClaimTenantResolver();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tenant_id", "\u0430cme-corp")
            ], "TestAuth"))
        };

        var tenantId = await resolver.ResolveTenantIdAsync(context);
        tenantId.ShouldBeNull();
    }

    [Fact]
    public async Task SubdomainResolver_ExtractsSubdomain()
    {
        var resolver = new KyrolusSubdomainTenantResolver();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("acme.api.example.com");

        var tenantId = await resolver.ResolveTenantIdAsync(context);

        tenantId.ShouldBe("acme");
    }

    [Fact]
    public async Task CompositeResolver_ResolvesInOrder()
    {
        var composite = new KyrolusCompositeTenantResolver([
            new KyrolusHeaderTenantResolver(),
            new KyrolusSubdomainTenantResolver()
        ]);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("subdomain-tenant.api.com");
        context.Request.Headers["X-Tenant-Id"] = "header-tenant";

        // Header has priority because it is first
        var tenantId = await composite.ResolveTenantIdAsync(context);
        tenantId.ShouldBe("header-tenant");
    }

    [Fact]
    public void DiRegistration_AddKyrolusMultiTenancy_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMultiTenancy();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusTenantContext>().ShouldNotBeNull();
        provider.GetService<IKyrolusTenantResolver>().ShouldNotBeNull();
    }

    [Fact]
    public async Task SubdomainResolver_IgnoresPortAndIpAddresses()
    {
        var resolver = new KyrolusSubdomainTenantResolver();

        // 1. IP address should return null
        var ipContext = new DefaultHttpContext();
        ipContext.Request.Host = new HostString("192.168.1.1:5000");
        var ipTenant = await resolver.ResolveTenantIdAsync(ipContext);
        ipTenant.ShouldBeNull();

        // 2. Subdomain with port should strip port and return tenant
        var portContext = new DefaultHttpContext();
        portContext.Request.Host = new HostString("tenant-corp.api.example.com:8443");
        var portTenant = await resolver.ResolveTenantIdAsync(portContext);
        portTenant.ShouldBe("tenant-corp");
    }

    [Fact]
    public async Task HeaderResolver_RejectsInvalidTenantCharacters()
    {
        var resolver = new KyrolusHeaderTenantResolver();

        // Path traversal / CRLF attack in header
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "../../../etc/passwd\r\n";

        var tenantId = await resolver.ResolveTenantIdAsync(context);
        tenantId.ShouldBeNull();
    }

    [Fact]
    public async Task TenantEndpointFilter_ForbidsUserWithoutTenantClaim()
    {
        var filter = new KyrolusTenantEndpointFilter();

        var services = new ServiceCollection();
        var tenantContext = new KyrolusTenantContext { TenantId = "tenant-xyz" };
        services.AddSingleton<IKyrolusTenantContext>(tenantContext);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            // Authenticated user with NO tenant claim
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "user-no-tenant")
            ], "TestScheme"))
        };

        var filterContext = new TestEndpointFilterInvocationContext(httpContext);

        var result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>("OK"));

        // Should return Forbid!
        result.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();
    }

    [Fact]
    public async Task CompositeResolver_FallsBack_WhenEarlierResolverThrows()
    {
        var throwingResolver = new ThrowingTenantResolver();
        var fallbackResolver = new KyrolusHeaderTenantResolver();

        var composite = new KyrolusCompositeTenantResolver([throwingResolver, fallbackResolver]);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-fallback";

        var tenantId = await composite.ResolveTenantIdAsync(context);

        tenantId.ShouldBe("tenant-fallback");
    }

    [Theory]
    [InlineData("\u0430cme")]
    [InlineData("t\u00e9nant")]
    [InlineData("tenant id")]
    [InlineData("tenant$id")]
    public async Task HeaderResolver_RejectsNonAsciiAndHomoglyphTenantIds(string invalidTenantId)
    {
        var resolver = new KyrolusHeaderTenantResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = invalidTenantId;

        var result = await resolver.ResolveTenantIdAsync(context);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task HeaderResolver_FallsBackToDefaultHeader_WhenNullOrWhitespace()
    {
        var resolver = new KyrolusHeaderTenantResolver("   ");
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-safe";

        var result = await resolver.ResolveTenantIdAsync(context);
        result.ShouldBe("tenant-safe");
    }

    [Fact]
    public async Task SubdomainResolver_RejectsNonAsciiSubdomain()
    {
        var resolver = new KyrolusSubdomainTenantResolver();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("\u0430cme.api.example.com");

        var result = await resolver.ResolveTenantIdAsync(context);
        result.ShouldBeNull();
    }

    private sealed class ThrowingTenantResolver : IKyrolusTenantResolver
    {
        public ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
        {
            throw new InvalidOperationException("External tenant resolution service failed.");
        }
    }

    private sealed class TestEndpointFilterInvocationContext : EndpointFilterInvocationContext
    {
        public TestEndpointFilterInvocationContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public override HttpContext HttpContext { get; }
        public override IList<object?> Arguments => [];
        public override T GetArgument<T>(int index) => default!;
    }
}
