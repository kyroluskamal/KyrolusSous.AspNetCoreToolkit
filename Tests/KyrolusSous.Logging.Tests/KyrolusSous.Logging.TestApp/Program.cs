using KyrolusSous.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKyrolusLogging(builder.Configuration, options =>
{
    options.ApplicationName = "TestApp";
});

builder.Host.UseKyrolusLogging();
var app = builder.Build();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Integration Test: Information Message, endpoint '/' was hit.");
    logger.LogWarning("Integration Test: Warning Message.");

    return "Hello from TestApp! Logging has been triggered.";
});

await app.RunAsync();
public partial class Program { }