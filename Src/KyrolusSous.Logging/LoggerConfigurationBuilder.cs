global using Serilog.Events;
global using Serilog.Core;
global using System.Reflection;
using KyrolusSous.Logging.Theming;
using static KyrolusSous.Logging.LoggingOptions;
namespace KyrolusSous.Logging
{
    /// <summary>
    /// Internal builder class responsible for applying configurations from LoggingOptions
    /// to a LoggerConfiguration using reflection.
    /// </summary>
    public static class LoggerConfigurationBuilder
    {
        private static readonly Dictionary<CommonSinkType, (string MethodName, string PackageName)> CommonSinkMap = new()
        {
            { CommonSinkType.Console, ("Console", "Console") },
            { CommonSinkType.File, ("File", "File") },
            { CommonSinkType.Seq, ("Seq", "Seq") },
            { CommonSinkType.MSSqlServer, ("MSSqlServer", "MSSqlServer") },
            { CommonSinkType.Elasticsearch, ("Elasticsearch", "Elasticsearch") },
            { CommonSinkType.PostgreSQL, ("PostgreSQL", "PostgreSQL") },
            { CommonSinkType.SQLite, ("SQLite", "SQLite") }
        };
        private static readonly Dictionary<CommonEnricherType, (string MethodName, string PackageName)> CommonEnricherMap = new()
        {
            { CommonEnricherType.FromLogContext, ("FromLogContext", "Serilog") },
            { CommonEnricherType.MachineName, ("WithMachineName", "Serilog.Enrichers.Environment") },
            { CommonEnricherType.EnvironmentUserName, ("WithEnvironmentUserName", "Serilog.Enrichers.Environment") },
            { CommonEnricherType.EnvironmentName, ("WithEnvironmentName", "Serilog.Enrichers.Environment") },
            { CommonEnricherType.ProcessId, ("WithProcessId", "Serilog.Enrichers.Process") },
            { CommonEnricherType.ProcessName, ("WithProcessName", "Serilog.Enrichers.Process") },
            { CommonEnricherType.ThreadId, ("WithThreadId", "Serilog.Enrichers.Thread") },
            { CommonEnricherType.ThreadName, ("WithThreadName", "Serilog.Enrichers.Thread") },
            { CommonEnricherType.HttpRequestId, ("WithHttpRequestId", "Serilog.AspNetCore") }
        };
        public static void Build(LoggerConfiguration loggerConfig, LoggingOptions options, IHostEnvironment environment)
        {
            loggerConfig
                .MinimumLevel.Is(options.MinimumLevel)
                .Enrich.WithProperty("Application", options.ApplicationName);

            foreach (var overrideRule in options.MinimumLevelOverrides)
            {
                loggerConfig.MinimumLevel.Override(overrideRule.Key, overrideRule.Value);
            }
            ApplyEnrichers(loggerConfig, options.Enrichers);
            ApplySinks(loggerConfig, options.Sinks, environment, options);
            ApplyFilters(loggerConfig, options.ExcludeByMessageSubstring, options.ExcludeBySourceContextPrefix);
        }

        #region Apply Logic
        /// <summary>
        /// Applies the configured enrichers to the logger configuration.
        /// This is the main entry point for applying all enricher configurations.
        /// </summary>
        private static void ApplyEnrichers(LoggerConfiguration loggerConfig, List<EnricherConfiguration> enricherConfigs)
        {

            foreach (var config in enricherConfigs)
            {
                if (config.CommonType == CommonEnricherType.FromLogContext)
                {
                    loggerConfig.Enrich.FromLogContext();
                    continue;
                }
                ProcessSingleEnricher(loggerConfig, config);
            }
        }
        /// <summary>
        /// Processes a single enricher configuration by determining its type and applying it.
        /// </summary>
        private static void ProcessSingleEnricher(LoggerConfiguration loggerConfig, LoggingOptions.EnricherConfiguration config)
        {
            if (config.CommonType.HasValue)
            {
                HandleCommonEnricher(loggerConfig, config);
                return;
            }
            if (config.CustomType != null)
            {
                HandleCustomEnricher(loggerConfig, config);
                return;
            }
            if (!string.IsNullOrEmpty(config.MethodName) && !string.IsNullOrEmpty(config.PackageName))
            {
                TryApplyMethod(loggerConfig.Enrich, config.MethodName, config.PackageName, config.Parameters);
            }
        }

        /// <summary>
        /// Handles the logic for applying a common enricher from the enum map.
        /// </summary>
        private static void HandleCommonEnricher(LoggerConfiguration loggerConfig, LoggingOptions.EnricherConfiguration config)
        {
            if (CommonEnricherMap.TryGetValue(config.CommonType!.Value, out var enricherInfo))
            {
                TryApplyMethod(loggerConfig.Enrich, enricherInfo.MethodName, enricherInfo.PackageName, config.Parameters);
            }
        }

        /// <summary>
        /// Handles the logic for instantiating and applying a custom enricher type.
        /// </summary>
        private static void HandleCustomEnricher(LoggerConfiguration loggerConfig, EnricherConfiguration config)
        {
            if (!typeof(ILogEventEnricher).IsAssignableFrom(config.CustomType))
            {
                throw new InvalidOperationException($"The provided CustomType '{config.CustomType?.FullName}' does not implement ILogEventEnricher.");
            }
            try
            {
#pragma warning disable S1944
                var enricherInstance = (ILogEventEnricher)Activator.CreateInstance(config.CustomType)!;
#pragma warning restore S1944
                loggerConfig.Enrich.With(enricherInstance);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not create an instance of the custom enricher '{config.CustomType!.FullName}'. Please ensure it has a parameterless constructor.", ex);
            }
        }
        /// <summary>
        /// The main entry point for applying sink configurations.
        /// </summary>
        private static void ApplySinks(LoggerConfiguration loggerConfig, List<SinkConfiguration> sinkConfigs, IHostEnvironment environment, LoggingOptions options)
        {

            foreach (var config in sinkConfigs)
            {
                ProcessSingleSink(loggerConfig, config, environment, options);
            }
        }

        /// <summary>
        /// Processes a single sink configuration. This is the final, correct version.
        /// </summary>
        private static void ProcessSingleSink(LoggerConfiguration loggerConfig, SinkConfiguration config, IHostEnvironment environment, LoggingOptions options)
        {
            if (config.CustomType != null)
            {
                HandleCustomSink(loggerConfig, config);
                return;
            }

            var (methodName, packageName) = GetSinkDetails(config);
            if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(packageName))
            {
                return;
            }

            var parameters = ConvertOptionsToDictionary(config.SinkOptions);

            PrepareSinkParameters(parameters, config.CommonType, environment, options);

            TryApplyMethod(loggerConfig.WriteTo, methodName, packageName, parameters, config.MinimumLevel);
        }

        /// <summary>
        /// A helper method to convert a strongly-typed options object (like FileSinkOptions) 
        /// or an existing dictionary into a unified dictionary format for processing.
        /// </summary>
        private static Dictionary<string, object?> ConvertOptionsToDictionary(object? options)
        {
            if (options == null)
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }

            if (options is IDictionary<string, object?> dictionary)
            {
                return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
            }

            return options.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(prop => ToCamelCase(prop.Name), prop => prop.GetValue(options), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Prepares the parameters dictionary by applying defaults and normalizing paths.
        /// </summary>
        private static void PrepareSinkParameters(Dictionary<string, object?> parameters, CommonSinkType sinkType, IHostEnvironment environment, LoggingOptions options)
        {
            if (!parameters.ContainsKey("formatter") && !parameters.ContainsKey("outputTemplate"))
            {
                parameters["outputTemplate"] = options.DefaultOutputTemplate;
            }

            if (sinkType == CommonSinkType.File && parameters.TryGetValue("path", out var pathValue) && pathValue is string path && !Path.IsPathRooted(path))
            {
                parameters["path"] = Path.Combine(environment.ContentRootPath, path);
            }
        }

        private static (string? MethodName, string? PackageName) GetSinkDetails(SinkConfiguration config)
        {
            if (config.CommonType != CommonSinkType.None)
            {
                if (CommonSinkMap.TryGetValue(config.CommonType, out var sinkInfo))
                {
                    return (sinkInfo.MethodName, $"Serilog.Sinks.{sinkInfo.PackageName}");
                }
            }
            else if (!string.IsNullOrEmpty(config.SinkMethodName) && !string.IsNullOrEmpty(config.SinkPackageName))
            {
                return (config.SinkMethodName, config.SinkPackageName);
            }
            return (null, null);
        }

        private static void HandleCustomSink(LoggerConfiguration loggerConfig, SinkConfiguration config)
        {
            try
            {
                if (!typeof(ILogEventSink).IsAssignableFrom(config.CustomType))
                {
                    throw new InvalidOperationException($"The provided CustomType '{config.CustomType?.FullName}' does not implement ILogEventSink.");
                }

#pragma warning disable S1944
                var sinkInstance = (ILogEventSink)Activator.CreateInstance(config.CustomType)!;
#pragma warning restore S1944
                loggerConfig.WriteTo.Sink(sinkInstance, config.MinimumLevel);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not create an instance of the custom sink '{config.CustomType!.FullName}'. Please ensure it has a parameterless constructor.", ex);
            }
        }
        private static void ApplyFilters(LoggerConfiguration loggerConfig, List<string> msgSubstrings, List<string> srcPrefixes)
        {
            if (msgSubstrings.Count != 0)
                loggerConfig.Filter.ByExcluding(le => msgSubstrings.Any(s => le.RenderMessage().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));

            if (srcPrefixes.Count != 0)
                loggerConfig.Filter.ByExcluding(le =>
                {
                    if (le.Properties.TryGetValue("SourceContext", out var sourceContext) && sourceContext is ScalarValue scalar)
                    {
                        return srcPrefixes.Any(p => scalar.Value?.ToString()?.StartsWith(p, StringComparison.OrdinalIgnoreCase) ?? false);
                    }
                    return false;
                });
        }
        #endregion

        #region Reflection Helpers
        /// <summary>
        /// Tries to find and invoke an extension method for a sink or enricher.
        /// Throws exceptions if required packages are missing.
        /// </summary>
        private static void TryApplyMethod(object configurationObject, string methodName, string assemblyName, IDictionary<string, object?> parameters, LogEventLevel? restrictedToMinimumLevel = null)
        {
            try
            {
                var loadedAssembly = Assembly.Load(new AssemblyName(assemblyName));

                // The type of the object we are configuring (e.g., LoggerSinkConfiguration)
                var configObjectType = configurationObject.GetType();

                var extensionTypes = loadedAssembly.GetExportedTypes()
                    .Where(t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("Extensions"))
                    .ToList();

                if (extensionTypes.Count == 0)
                {
                    // Fallback for core enrichers
                    extensionTypes.AddRange(typeof(ILogger).Assembly.GetExportedTypes()
                       .Where(t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("Extensions")));
                }

                if (extensionTypes.Count == 0)
                {
                    throw new InvalidOperationException($"Could not find any '...Extensions' classes in the package '{assemblyName}'.");
                }

                // --- THE CRUCIAL FIX IS HERE ---
                var methods = extensionTypes
                    .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    .Where(m =>
                        // 1. Method name must match.
                        m.Name == methodName &&
                        // 2. It must be an extension method.
                        m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false) &&
                        // 3. Its FIRST parameter (the 'this' parameter) must be compatible
                        //    with the configuration object we are passing.
                        m.GetParameters().Length > 0 &&
                        m.GetParameters()[0].ParameterType.IsAssignableFrom(configObjectType)
                    )
                    .ToList();

                if (methods.Count == 0)
                {
                    throw new InvalidOperationException($"Could not find method '{methodName}' in '{assemblyName}' that extends type '{configObjectType.Name}'.");
                }

                if (restrictedToMinimumLevel.HasValue)
                {
                    parameters["restrictedToMinimumLevel"] = restrictedToMinimumLevel.Value;
                }

                var (bestMethod, sortedArgs) = FindBestMethodOverload(methods, configurationObject, parameters);

                if (bestMethod == null)
                {
                    throw new InvalidOperationException($"Could not find an overload for method '{methodName}' that matches the provided parameters: [{string.Join(", ", parameters.Keys)}].");
                }

                bestMethod.Invoke(null, [.. sortedArgs!]);
            }
            catch (FileNotFoundException)
            {
                throw new InvalidOperationException($"The NuGet package '{assemblyName}' is required for method '{methodName}', but was not found.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An error occurred while calling the '{methodName}' method. Check inner exception for details.", ex.InnerException ?? ex);
            }
        }
        private static (MethodInfo? BestMethod, List<object?>? SortedArgs) FindBestMethodOverload(List<MethodInfo> methods, object configObject, IDictionary<string, object?> providedParams)
        {
            MethodInfo? bestMatch = null;
            List<object?>? bestMatchArgs = null;
            int bestMatchScore = -1;

            foreach (var method in methods)
            {
                if (TryGetArgumentsForMethod(method, providedParams, out var callArguments, out var score) && score > bestMatchScore)
                {
                    bestMatch = method;
                    bestMatchArgs = callArguments;
                    bestMatchScore = score;
                }
            }

            if (bestMatch != null && bestMatchArgs != null)
            {
                var finalArgs = new List<object?> { configObject };
                finalArgs.AddRange(bestMatchArgs);
                return (bestMatch, finalArgs);
            }

            return (null, null);
        }

        /// <summary>
        /// Tries to build a list of arguments for a method based on user-provided parameters.
        /// </summary>
        private static bool TryGetArgumentsForMethod(MethodInfo method, IDictionary<string, object?> userProvidedParams, out List<object?> arguments, out int score)
        {
            arguments = new List<object?>();
            score = 0;

            // Skip the first 'this' parameter of the extension method
            foreach (var pInfo in method.GetParameters().Skip(1))
            {
                // Delegate the logic for finding and converting a single parameter to a helper method
                if (!TryProcessSingleParameter(pInfo, userProvidedParams, out var argumentValue, ref score))
                {
                    // If processing any parameter fails, the whole method is not a match.
                    return false;
                }
                arguments.Add(argumentValue);
            }
            return true;
        }

        /// <summary>
        /// Processes a single parameter to find its value from the user-provided parameters or its default value.
        /// </summary>
        private static bool TryProcessSingleParameter(ParameterInfo pInfo, IDictionary<string, object?> userProvidedParams, out object? argumentValue, ref int score)
        {
            var matchingParamKey = userProvidedParams.Keys.FirstOrDefault(k => k.Equals(pInfo.Name, StringComparison.OrdinalIgnoreCase));

            // Case 1: The user provided a matching parameter.
            if (matchingParamKey != null)
            {
                var value = userProvidedParams[matchingParamKey];
                if (TryConvertParameter(value, pInfo.ParameterType, out argumentValue))
                {
                    score++;
                    return true;
                }
                return false;
            }
            // Case 2: The user did not provide the parameter, check if it's optional.
            if (pInfo.HasDefaultValue)
            {
                argumentValue = pInfo.DefaultValue;
                return true;
            }
            // Case 3: A required parameter is missing.
            argumentValue = null;
            return false;
        }

        /// <summary>
        /// Tries to convert a given value to the target parameter type.
        /// </summary>
        private static bool TryConvertParameter(object? value, Type targetType, out object? convertedValue)
        {
            convertedValue = null;
            if (value != null && targetType.IsInstanceOfType(value))
            {
                convertedValue = value;
                return true;
            }
            try
            {
                if (value is IConvertible && targetType.IsEnum)
                {
                    convertedValue = Enum.ToObject(targetType, Convert.ToInt32(value));
                }
                else
                {
                    convertedValue = Convert.ChangeType(value, targetType);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// A simple helper method to convert a string from PascalCase to camelCase.
        /// Example: "OutputTemplate" becomes "outputTemplate".
        /// </summary>
        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name, 0))
            {
                return name;
            }
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
        #endregion
    }


}