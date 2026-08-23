using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Localization;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class ExceptionHandlingExtensionTests
{
    [Fact(DisplayName = "AddKyrolusExceptionHandling should register all core dependencies")]
    public void AddKyrolusExceptionHandling_Should_Register_Core_Dependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());

        services.AddKyrolusExceptionHandling();

        var provider = services.BuildServiceProvider();

        provider.GetService<KyrolusHttpErrorContextFactory>().ShouldNotBeNull();
        provider.GetService<KyrolusExceptionMappingService>().ShouldNotBeNull();
        provider.GetService<IKyrolusErrorLocalizer>().ShouldBeOfType<KyrolusNullErrorLocalizer>();
        provider.GetService<IKyrolusErrorMetadataSanitizer>().ShouldBeOfType<KyrolusDefaultErrorMetadataSanitizer>();
        provider.GetService<IKyrolusErrorResponseWriter>().ShouldBeOfType<KyrolusJsonErrorResponseWriter>();
        provider.GetService<KyrolusExceptionTranslator>().ShouldNotBeNull();
        provider.GetService<KyrolusExceptionHandlingDependencies>().ShouldNotBeNull();
        provider.GetService<KyrolusExceptionFilter>().ShouldNotBeNull();

        var mappers = provider.GetServices<IKyrolusExceptionMapper>().ToArray();
        mappers.Length.ShouldBe(3);
        mappers.ShouldContain(m => m is KyrolusDomainExceptionMapper);
        mappers.ShouldContain(m => m is KyrolusFrameworkExceptionMapper);
        mappers.ShouldContain(m => m is KyrolusDefaultExceptionMapper);
    }

    [Fact(DisplayName = "AddKyrolusExceptionHandling should configure custom options when provided")]
    public void AddKyrolusExceptionHandling_Should_Apply_Custom_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());

        services.AddKyrolusExceptionHandling(options =>
        {
            options.IncludeTraceId = false;
            options.LogUnhandledExceptions = false;
            options.CorrelationIdHeaderName = "X-Custom-Correlation";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KyrolusExceptionHandlingOptions>>().Value;

        options.IncludeTraceId.ShouldBeFalse();
        options.LogUnhandledExceptions.ShouldBeFalse();
        options.CorrelationIdHeaderName.ShouldBe("X-Custom-Correlation");
    }

    [Fact(DisplayName = "AddKyrolusBuiltInExceptionHandlers should register all 10 built-in exception handlers")]
    public void AddKyrolusBuiltInExceptionHandlers_Should_Register_All_10_Handlers()
    {
        var services = new ServiceCollection();

        services.AddKyrolusBuiltInExceptionHandlers();

        var handlerDescriptors = services
            .Where(d => d.ServiceType == typeof(IExceptionHandler))
            .Select(d => d.ImplementationType)
            .ToArray();

        handlerDescriptors.Length.ShouldBe(10);
        handlerDescriptors.ShouldContain(typeof(CultureNotFoundExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(JsonExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(ArgumentExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(SocketExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(HttpRequestExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(TimeoutExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(NotFoundExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(UnauthorizedExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(SslAuthenticationExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(GeneralExceptionHandler));
    }

    [Fact(DisplayName = "UseKyrolusExceptionHandling should register middleware in application builder")]
    public void UseKyrolusExceptionHandling_Should_Register_Middleware()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddKyrolusExceptionHandling();
        var serviceProvider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);

        var result = app.UseKyrolusExceptionHandling();

        result.ShouldNotBeNull();
        result.ShouldBeSameAs(app);
    }

    [Fact(DisplayName = "AddKyrolusExceptionHandlingLocalization with dictionary should register DictionaryErrorLocalizer")]
    public void AddKyrolusExceptionHandlingLocalization_Dictionary_Should_Register_Localizer()
    {
        var services = new ServiceCollection();
        var translations = new Dictionary<string, string>
        {
            ["not_found"] = "Element non trouvé"
        };

        services.AddKyrolusExceptionHandlingLocalization(translations);

        var provider = services.BuildServiceProvider();
        var localizer = provider.GetService<IKyrolusErrorLocalizer>();

        localizer.ShouldNotBeNull();
        localizer.ShouldBeOfType<KyrolusDictionaryErrorLocalizer>();
        localizer.Localize("not_found", "Default", null).ShouldBe("Element non trouvé");
    }

    [Fact(DisplayName = "AddKyrolusExceptionHandlingLocalization with generic resource should register StringLocalizerErrorLocalizer")]
    public void AddKyrolusExceptionHandlingLocalization_Resource_Should_Register_Localizer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStringLocalizer<ITestSharedResource>>(new TestTypedStringLocalizer<ITestSharedResource>(
            new Dictionary<string, string> { ["forbidden"] = "Accès refusé" }));

        services.AddKyrolusExceptionHandlingLocalization<ITestSharedResource>();

        var provider = services.BuildServiceProvider();
        var localizer = provider.GetService<IKyrolusErrorLocalizer>();

        localizer.ShouldNotBeNull();
        localizer.ShouldBeOfType<KyrolusStringLocalizerErrorLocalizer>();
        localizer.Localize("forbidden", "Default", null).ShouldBe("Accès refusé");
    }
}
