global using System.Net;
global using System.Security.Authentication;
global using System.Security.Claims;
global using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
global using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;
global using KyrolusSous.ExceptionHandling.Runtime;
global using KyrolusSous.ExceptionHandling.Runtime.ClasesAndHelpers;
global using KyrolusSous.ExceptionHandling.Runtime.Handlers;
global using KyrolusSous.ExceptionHandling.Runtime.Interfaces;
global using KyrolusSous.ExceptionHandling.Runtime.Mapping;
global using KyrolusSous.ExceptionHandling.Runtime.Writers;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Shouldly;
global using Xunit;

[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
