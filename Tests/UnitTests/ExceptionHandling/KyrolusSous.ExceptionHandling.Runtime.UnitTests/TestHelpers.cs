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