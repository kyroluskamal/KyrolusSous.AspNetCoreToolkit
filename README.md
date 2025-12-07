# KyrolusSous.Logging

Opinionated Serilog bootstrapper with sane defaults, a flexible options model, and a customizable console formatter.

## What you get
- Extension methods: `AddKyrolusLogging(IConfiguration, Action<LoggingOptions>?)` and `UseKyrolusLogging(IHostBuilder)`.
- Defaults: enrichers (Application, MachineName, ProcessId, ThreadId, EnvironmentName) + sinks (Console, File: `Logs/log-.txt`).
- Reflection wiring: supports common sinks/enrichers by enum, plus custom types/methods.
- Strictness toggle: `ThrowIfPackageMissing` (default true) to fail fast if a sink/enricher package is missing.
- Console formatter: colored, per-sink configurable via `TextFormatterOptions` (properties, source, exception detail per level, colors).

## Install the needed Serilog packages
You must reference the sinks/enrichers you actually use. Typical minimum:
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```
Optional: `Serilog.Sinks.Seq`, `Serilog.Sinks.MSSqlServer`, `Serilog.Sinks.Elasticsearch`, `Serilog.Sinks.PostgreSQL`, `Serilog.Sinks.SQLite`, plus enrichers like `Serilog.Enrichers.Thread`, `Serilog.Enrichers.Process`, `Serilog.Enrichers.Environment`.

## Quick start (code-first)
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKyrolusLogging(builder.Configuration, opts =>
{
    // Optional: relax strictness in production
    opts.ThrowIfPackageMissing = false;

    // Customize formatter per sink (by name/type)
    opts.FormatterOptionsBySink["Console"] = new TextFormatterOptions
    {
        UseColors = true,
        ShowProperties = false,
        ExceptionDetail = TextFormatterOptions.ExceptionDetailLevel.MessageOnly,
        ExceptionDetailByLevel =
        {
            [LogEventLevel.Error] = TextFormatterOptions.ExceptionDetailLevel.Full,
            [LogEventLevel.Fatal] = TextFormatterOptions.ExceptionDetailLevel.Full
        }
    };
});

builder.Host.UseKyrolusLogging();
var app = builder.Build();
app.MapGet("/", (ILogger<Program> log) => { log.LogInformation("Hello"); return "OK"; });
app.Run();
```

## Config via appsettings (optional)
`AddKyrolusLogging` binds from the `Logging` section.
```json
"Logging": {
  "MinimumLevel": "Information",
  "Sinks": [
    { "commonType": "Console" },
    { "commonType": "File", "sinkOptions": { "path": "Logs/log-.txt", "rollingInterval": "Day" } },
    { "commonType": "Seq", "sinkOptions": { "serverUrl": "http://localhost:5341" } }
  ],
  "FormatterOptionsBySink": {
    "Console": {
      "useColors": true,
      "showProperties": false,
      "exceptionDetail": "MessageOnly",
      "exceptionDetailByLevel": { "Error": "Full", "Fatal": "Full" }
    }
  }
}
```

## Formatter options (per sink)
Set via `LoggingOptions.FormatterOptionsBySink["Console"]` or `DefaultFormatterOptions`:
- `UseColors`, `ShowProperties`, `ShowSourceContext`, `ShowException`
- `LevelColors` (per LogEventLevel ANSI codes)
- `ExceptionDetail` and `ExceptionDetailByLevel` (`None`, `MessageOnly`, `TypeAndMessage`, `Full`)

If no formatter is provided for the Console sink, a `CustomTextFormatter` is injected using the per-sink or default options.

## Sink support notes
- Common sinks advertised: Console, File, Seq, MSSqlServer, Elasticsearch, PostgreSQL, SQLite.
- Only Console/File have typed options here; others are “Advanced” and require the corresponding Serilog sink package plus `SinkOptions` dictionary/typed options from that package.
- If a required package is missing: with `ThrowIfPackageMissing=true` the app fails fast with a clear message; otherwise it logs a debug warning and skips the sink/enricher.

## Tests
See `Tests/KyrolusSous.Logging.Tests` for unit/integration coverage of defaults, file sink, custom sinks, and configuration behavior.
