using Microsoft.Extensions.Localization;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public sealed class TestLogger : ILogger
{
    public bool Enabled { get; set; } = true;
    public List<(LogLevel Level, string Message)> Logs { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => Enabled;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Logs.Add((logLevel, formatter(state, exception)));
    }
}

public sealed class TestLogger<T> : ILogger<T>
{
    public bool Enabled { get; set; } = true;
    public List<(LogLevel Level, string Message)> Logs { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => Enabled;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Logs.Add((logLevel, formatter(state, exception)));
    }
}

public sealed class TestHostEnvironment(string environmentName = "Production") : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "TestApp";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public interface ITestSharedResource;

public sealed class TestStringLocalizer(IReadOnlyDictionary<string, string> translations) : IStringLocalizer
{
    public LocalizedString this[string name]
    {
        get
        {
            var found = translations.TryGetValue(name, out var value);
            return new LocalizedString(name, value ?? name, !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
        => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => translations.Select(t => new LocalizedString(t.Key, t.Value, false));
}

public sealed class TestTypedStringLocalizer<TResource>(IReadOnlyDictionary<string, string> translations)
    : IStringLocalizer<TResource>
{
    public LocalizedString this[string name]
    {
        get
        {
            var found = translations.TryGetValue(name, out var value);
            return new LocalizedString(name, value ?? name, !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
        => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => translations.Select(t => new LocalizedString(t.Key, t.Value, false));
}

/// <summary>
/// Single place to hand-wire a <see cref="KyrolusExceptionHandlingDependencies"/> for tests that want to bypass
/// the DI container (e.g. to assert on a specific <see cref="TestLogger{T}"/> instance directly). Every test that
/// exercises the middleware, the MVC filter, or a native <see cref="IExceptionHandler"/> in isolation needs the
/// exact same mapper/sanitizer/translator/writer wiring, so it lives here once instead of being retyped per file.
/// </summary>
public static class TestExceptionHandlingDependenciesFactory
{
    public static KyrolusExceptionHandlingDependencies Create(
        ILogger<KyrolusExceptionHandlingDependencies>? logger = null,
        Action<KyrolusExceptionHandlingOptions>? configureOptions = null,
        string environmentName = "Development",
        IKyrolusLocalizer? localizer = null)
    {
        var options = new KyrolusExceptionHandlingOptions();
        configureOptions?.Invoke(options);
        var optionsWrapper = Options.Create(options);

        var contextFactory = new KyrolusHttpErrorContextFactory(optionsWrapper);
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper(),
            new KyrolusDefaultExceptionMapper()
        };
        var mappingService = new KyrolusExceptionMappingService(mappers);
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(optionsWrapper);
        var environment = new TestHostEnvironment(environmentName);
        var translator = new KyrolusExceptionTranslator(mappingService, sanitizer, environment, optionsWrapper, localizer);
        var writer = new KyrolusJsonErrorResponseWriter();

        return new KyrolusExceptionHandlingDependencies(
            translator,
            writer,
            contextFactory,
            optionsWrapper,
            logger ?? NullLogger<KyrolusExceptionHandlingDependencies>.Instance);
    }
}