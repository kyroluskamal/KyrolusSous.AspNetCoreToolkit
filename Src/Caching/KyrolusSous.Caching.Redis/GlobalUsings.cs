global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.IO.Compression;
global using System.Text;
global using KyrolusSous.Caching.Abstractions;
global using KyrolusSous.Compression;
global using Microsoft.AspNetCore.OutputCaching;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using StackExchange.Redis;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KyrolusSous.Caching.UnitTests")]

