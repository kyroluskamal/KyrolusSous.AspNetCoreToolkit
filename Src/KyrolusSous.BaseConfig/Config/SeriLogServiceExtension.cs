global using Microsoft.AspNetCore.Builder;
using System.Diagnostics;
using KyrolusSous.BaseConfig.LoggingService;
using Microsoft.Extensions.Hosting;
using Serilog;
namespace KyrolusSous.BaseConfig.Config;

public static class SeriLogServiceExtension
{
    public static void AddSeriLogServices(this IHostBuilder host)
    {
        host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                                  .Enrich.FromLogContext()
                                  .Enrich.WithProperty("Application", "CodingBible")
                                  .Enrich.WithProperty("MachineName", Environment.MachineName)
                                  .Enrich.WithProperty("CurrentManagedThreadId", Environment.CurrentManagedThreadId)
                                  .Enrich.WithProperty("OSVersion", Environment.OSVersion)
                                  .Enrich.WithProperty("Version", Environment.Version)
                                  .Enrich.WithProperty("UserName", Environment.UserName)
                                  .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                                  .Enrich.WithProperty("ProcessName", Process.GetCurrentProcess().ProcessName)
                                  .WriteTo.Console(theme: CustomAnsiConsoleTheme.VisualStudioMacLight)
                                  .WriteTo.File(
                                      formatter: new CustomTextFormatter(),
                                      path: Path.Combine(hostingContext.HostingEnvironment.ContentRootPath, "Logs", "lg.txt"),
                                      rollingInterval: RollingInterval.Day
                                  )
                                  .ReadFrom.Configuration(hostingContext.Configuration));
    }
}
