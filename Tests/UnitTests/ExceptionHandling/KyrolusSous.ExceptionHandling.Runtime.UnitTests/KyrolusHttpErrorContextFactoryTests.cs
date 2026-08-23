using System.Diagnostics;
using Microsoft.AspNetCore.Localization;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class KyrolusHttpErrorContextFactoryTests
{
    [Fact(DisplayName = "Create with null context and null accessor should return default context")]
    public void Create_With_NullContext_And_NullAccessor_Should_Return_Defaults()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var factory = new KyrolusHttpErrorContextFactory(options, accessor: null);

        var context = factory.Create(context: null);

        context.ShouldNotBeNull();
        context.Path.ShouldBeNull();
        context.Method.ShouldBeNull();
        context.UserId.ShouldBeNull();
        context.TenantId.ShouldBeNull();
        context.Culture.ShouldBeNull();
    }

    [Fact(DisplayName = "Create with accessor containing HttpContext should extract context details")]
    public void Create_With_Accessor_Should_Extract_Details()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/products";
        httpContext.Request.Method = "POST";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var factory = new KyrolusHttpErrorContextFactory(options, accessor);

        var context = factory.Create(context: null);

        context.Path.ShouldBe("/api/products");
        context.Method.ShouldBe("POST");
    }

    [Fact(DisplayName = "Create should extract correlationId from custom header")]
    public void Create_Should_Extract_CorrelationId_From_Custom_Header()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions
        {
            CorrelationIdHeaderName = "X-Correlation-ID"
        });
        var factory = new KyrolusHttpErrorContextFactory(options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-header-123";

        var context = factory.Create(httpContext);

        context.CorrelationId.ShouldBe("corr-header-123");
    }

    [Fact(DisplayName = "Create should fallback correlationId to TraceIdentifier when header is missing")]
    public void Create_Should_Fallback_CorrelationId_To_TraceIdentifier()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var factory = new KyrolusHttpErrorContextFactory(options);

        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-id-999" };

        var context = factory.Create(httpContext);

        context.CorrelationId.ShouldBe("trace-id-999");
    }

    [Fact(DisplayName = "Create should return null correlationId when IncludeCorrelationId is false")]
    public void Create_Should_Return_Null_CorrelationId_When_Disabled()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions
        {
            IncludeCorrelationId = false
        });
        var factory = new KyrolusHttpErrorContextFactory(options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-123";

        var context = factory.Create(httpContext);

        context.CorrelationId.ShouldBeNull();
    }

    [Fact(DisplayName = "Create should extract culture from IRequestCultureFeature")]
    public void Create_Should_Extract_Culture_From_RequestCultureFeature()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var factory = new KyrolusHttpErrorContextFactory(options);

        var httpContext = new DefaultHttpContext();
        var feature = new RequestCultureFeature(new RequestCulture("fr-FR"), null);
        httpContext.Features.Set<IRequestCultureFeature>(feature);

        var context = factory.Create(httpContext);

        context.Culture.ShouldNotBeNull();
        context.Culture.Name.ShouldBe("fr-FR");
    }

    [Fact(DisplayName = "Create should extract culture from Accept-Language header when feature is missing")]
    public void Create_Should_Extract_Culture_From_AcceptLanguage_Header()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions());
        var factory = new KyrolusHttpErrorContextFactory(options);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Accept-Language"] = "invalid_culture, ar-EG;q=0.9, en-US;q=0.8";

        var context = factory.Create(httpContext);

        context.Culture.ShouldNotBeNull();
        context.Culture.Name.ShouldBe("ar-EG");
    }

    [Fact(DisplayName = "Create should extract claims for userId and tenantId")]
    public void Create_Should_Extract_UserId_And_TenantId_Claims()
    {
        var options = Options.Create(new KyrolusExceptionHandlingOptions
        {
            UserIdClaimType = ClaimTypes.NameIdentifier,
            TenantIdClaimType = "tenant_id"
        });
        var factory = new KyrolusHttpErrorContextFactory(options);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-42"),
            new("tenant_id", "tenant-corp")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var context = factory.Create(httpContext);

        context.UserId.ShouldBe("user-42");
        context.TenantId.ShouldBe("tenant-corp");
    }
}
