// <copyright file="GlobalUsings.cs" company="KyrolusSous">
// Copyright (c) KyrolusSous. All rights reserved.
// </copyright>

global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Text;
global using System.Text.Json;
global using System.Threading.Tasks;
global using KyrolusSous.Logging.Abstractions;
global using KyrolusSous.Logging.Abstractions.Attributes;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("KyrolusSous.Logging.UnitTests")]
[assembly: InternalsVisibleTo("KyrolusSous.Logging.IntegrationTests")]
