global using System.Diagnostics.CodeAnalysis;
global using System.IO.Compression;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.Json.Serialization.Metadata;
global using KyrolusSous.Caching.Abstractions;
global using KyrolusSous.Caching.MessagePack;
global using KyrolusSous.Caching.Redis;
global using KyrolusSous.Compression;
global using MessagePack;
global using Microsoft.AspNetCore.OutputCaching;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using NSubstitute;
global using Shouldly;
global using StackExchange.Redis;
global using Xunit;

[assembly: ExcludeFromCodeCoverage]
