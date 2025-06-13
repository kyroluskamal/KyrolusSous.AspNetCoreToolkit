// MediatorGenerator.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

// Define the namespace for the generator project
namespace KyrolusSous.SourceMediator.Generator
{
    [Generator] // Mark this class as a Source Generator
    public class MediatorGenerator : IIncrementalGenerator
    {
        // --- Constants for necessary fully qualified type names ---
        // These names must exactly match the types defined in the SourceMediator runtime/interfaces library.
        private const string QueryHandlerInterfaceFullName = "SourceMediator.Interfaces.IQueryHandler`2";
        private const string CommandHandlerInterfaceFullName = "SourceMediator.Interfaces.ICommandHandler`1";
        private const string CommandHandlerWithResponseInterfaceFullName = "SourceMediator.Interfaces.ICommandHandler`2";
        private const string UnitTypeFullName = "SourceMediator.Interfaces.Unit";
        // This interface is expected to be defined as 'internal' in the SourceMediator runtime library
        private const string GeneratedDispatcherInterfaceFullName = "SourceMediator.Interfaces.IGeneratedDispatcher";

        /// <summary>
        /// Called by the compiler to initialize the incremental generator pipeline.
        /// </summary>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // STEP 1: Define the pipeline stage to find potential handler classes.
            // Filters syntax nodes for concrete (non-abstract, non-static) classes 
            // that have a base list (might implement interfaces or inherit).
            // Transforms the syntax node into its semantic symbol (INamedTypeSymbol).
            IncrementalValuesProvider<INamedTypeSymbol> handlerClassSymbols = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null, AttributeLists.Count: >= 0 },
                    transform: static (ctx, ct) => GetSemanticTargetForGeneration(ctx, ct))
                .Where(static symbol => symbol is not null)!; // Ensure we only pass non-null symbols

            // STEP 2: Combine the stream of found handler symbols with the Compilation object.
            // The Compilation object is needed in the execution phase to resolve types.
            IncrementalValueProvider<(Compilation Compilation, ImmutableArray<INamedTypeSymbol> HandlerSymbols)> compilationAndHandlers
                = context.CompilationProvider.Combine(handlerClassSymbols.Collect());

            // STEP 3: Register the final execution step (Execute method) 
            // to be called with the combined compilation and handler symbols.
            context.RegisterSourceOutput(compilationAndHandlers, Execute);
        }

        /// <summary>
        /// Semantic filter executed only for nodes passing the predicate.
        /// Returns the INamedTypeSymbol if it represents a concrete class, otherwise null.
        /// </summary>
        private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
            // Get the semantic symbol representing the class definition
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken) as INamedTypeSymbol;

            // Ignore interfaces, structs, enums, delegates, abstract classes, static classes, or if symbol is null
            if (classSymbol == null || classSymbol.TypeKind != TypeKind.Class || classSymbol.IsAbstract || classSymbol.IsStatic)
            {
                return null;
            }
            // Return the symbol for the concrete class
            return classSymbol;
        }

        /// <summary>
        /// Main generation method, executed with the compilation and collected handler symbols.
        /// </summary>
        private static void Execute(SourceProductionContext context, (Compilation Compilation, ImmutableArray<INamedTypeSymbol> HandlerSymbols) source)
        {
            var (compilation, handlerSymbols) = source; // Deconstruct the tuple
            if (handlerSymbols.IsDefaultOrEmpty) return; // Exit if no potential handlers found

            // --- Get required base symbols from the compilation context ---
            // These are needed for analysis and comparison later.
            INamedTypeSymbol? unitSymbol = compilation.GetTypeByMetadataName(UnitTypeFullName);
            INamedTypeSymbol? queryHandlerDef = compilation.GetTypeByMetadataName(QueryHandlerInterfaceFullName);
            INamedTypeSymbol? commandHandlerDef = compilation.GetTypeByMetadataName(CommandHandlerInterfaceFullName);
            INamedTypeSymbol? commandHandlerWithResponseDef = compilation.GetTypeByMetadataName(CommandHandlerWithResponseInterfaceFullName);
            INamedTypeSymbol? generatedDispatcherInterfaceSymbol = compilation.GetTypeByMetadataName(GeneratedDispatcherInterfaceFullName);

            // --- Crucial Check: Ensure all base types/interfaces are resolvable ---
            // If any of these are null, it likely means the consuming project is not referencing 
            // the SourceMediator runtime library correctly. Report an error.
            if (unitSymbol == null || queryHandlerDef == null || commandHandlerDef == null || commandHandlerWithResponseDef == null || generatedDispatcherInterfaceSymbol == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "SMG001",
                        title: "SourceMediator base types/interfaces not found",
                        messageFormat: "Could not find required SourceMediator types ({0}). Ensure the project references the SourceMediator library correctly.",
                        category: "SourceMediator.Generator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    string.Join(", ", new[] { // List the types being checked
                        UnitTypeFullName, QueryHandlerInterfaceFullName, CommandHandlerInterfaceFullName,
                        CommandHandlerWithResponseInterfaceFullName, GeneratedDispatcherInterfaceFullName
                    }.Where(s => compilation.GetTypeByMetadataName(s) == null)) // Only show missing ones
                ));
                return; // Stop generation if base types are missing
            }

            // --- Analyze handler symbols and collect detailed information ---
            var handlerInfos = new List<HandlerInfo>();
            // HashSet to store unique namespaces required for 'using' statements in generated code
            var namespaces = new HashSet<string>
            {
                "System", "System.Threading", "System.Threading.Tasks",
                "System.Collections.Generic", "Microsoft.Extensions.DependencyInjection",
                "SourceMediator.Interfaces", // Namespace for ICommand, IQuery etc.
                "SourceMediator.Generated", // Namespace for the generated static dispatcher
                generatedDispatcherInterfaceSymbol.ContainingNamespace.ToDisplayString() // Namespace for IGeneratedDispatcher (e.g., SourceMediator)
            };

            // Iterate through the concrete class symbols identified in the Initialize pipeline
            foreach (var handlerSymbol in handlerSymbols)
            {
                context.CancellationToken.ThrowIfCancellationRequested(); // Check for cancellation

                // Call helper method to analyze the symbol and extract handler details
                var handlerInfo = TryGetHandlerInfo(handlerSymbol, unitSymbol, queryHandlerDef, commandHandlerDef, commandHandlerWithResponseDef);
                if (handlerInfo != null)
                {
                    handlerInfos.Add(handlerInfo);
                    // Collect namespaces from the types involved for 'using' statements
                    CollectNamespaces(handlerInfo.HandlerType, namespaces);
                    CollectNamespaces(handlerInfo.RequestType, namespaces);
                    CollectNamespaces(handlerInfo.ResponseType, namespaces);
                }
                // Optional: Add diagnostic warning if a class looks like a handler but doesn't match perfectly?
            }

            // --- Generate the source code files if any valid handlers were found ---
            if (handlerInfos.Count > 0)
            {
                // 1. Generate the static dispatcher class (contains the core logic)
                string staticDispatcherCode = GenerateStaticDispatcherCode(handlerInfos, unitSymbol, namespaces);
                context.AddSource("SourceMediator.GeneratedDispatcher.g.cs", SourceText.From(staticDispatcherCode, Encoding.UTF8));

                // 2. Generate the class implementing the internal IGeneratedDispatcher interface
                string dispatcherImplCode = GenerateDispatcherImplementation(generatedDispatcherInterfaceSymbol);
                context.AddSource("SourceMediator.GeneratedDispatcherImpl.g.cs", SourceText.From(dispatcherImplCode, Encoding.UTF8));

                // 3. Generate the DI extension method to register the implementation
                string diExtensionCode = GenerateDIRegistration(generatedDispatcherInterfaceSymbol);
                context.AddSource("SourceMediator.GeneratedDIExtensions.g.cs", SourceText.From(diExtensionCode, Encoding.UTF8));

                // 4. Generate the DI extension method for Handler Registration ****
                string handlersDiExtensionCode = GenerateHandlerRegistrationMethod(handlerInfos, namespaces);
                context.AddSource("SourceMediator.GeneratedHandlersDIExtensions.g.cs", SourceText.From(handlersDiExtensionCode, Encoding.UTF8));
            }
        }

        // --- Helper Record ---
        /// <summary>Helper record to store extracted information about a specific handler.</summary>
        private record HandlerInfo(INamedTypeSymbol HandlerType, INamedTypeSymbol RequestType, ITypeSymbol ResponseType, string InterfaceFullName);

        // --- Helper Methods ---

        /// <summary>
        /// Analyzes a class symbol to determine if it implements one of the SourceMediator handler interfaces
        /// and extracts the request and response types.
        /// </summary>
        private static HandlerInfo? TryGetHandlerInfo(
            INamedTypeSymbol handlerSymbol, INamedTypeSymbol unitSymbol,
            INamedTypeSymbol queryHandlerDef, INamedTypeSymbol commandHandlerDef, INamedTypeSymbol commandHandlerWithResponseDef)
        {
            // Iterate through all interfaces (including inherited ones)
            foreach (var iface in handlerSymbol.AllInterfaces)
            {
                // Ensure it's a constructed generic type (e.g., ICommandHandler<MyCommand, Unit>)
                if (!iface.IsGenericType || iface.ConstructedFrom == null) continue;

                var originalDef = iface.OriginalDefinition; // Get the unbound generic definition (e.g., ICommandHandler<,>)
                INamedTypeSymbol? reqType = null;
                ITypeSymbol? resType = null;
                string ifaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); // Get full name like ICommandHandler<NS.Req, NS.Resp>

                // Compare original definition with the target handler definitions
                if (SymbolEqualityComparer.Default.Equals(originalDef, queryHandlerDef) && iface.TypeArguments.Length == 2)
                { reqType = iface.TypeArguments[0] as INamedTypeSymbol; resType = iface.TypeArguments[1]; }
                else if (SymbolEqualityComparer.Default.Equals(originalDef, commandHandlerWithResponseDef) && iface.TypeArguments.Length == 2)
                { reqType = iface.TypeArguments[0] as INamedTypeSymbol; resType = iface.TypeArguments[1]; }
                else if (SymbolEqualityComparer.Default.Equals(originalDef, commandHandlerDef) && iface.TypeArguments.Length == 1)
                { reqType = iface.TypeArguments[0] as INamedTypeSymbol; resType = unitSymbol; /* Assign Unit type */ }

                // If a match was found and types were extracted successfully
                if (reqType != null && resType != null)
                {
                    // TODO (Optional advanced check): Verify handlerSymbol has accessible constructor for DI
                    return new HandlerInfo(handlerSymbol, reqType, resType, ifaceName);
                }
            }
            return null; // Not a recognized handler interface implementation
        }

        /// <summary>
        /// Generates the source code for the static GeneratedMediatorDispatcher class.
        /// </summary>
        private static string GenerateStaticDispatcherCode(List<HandlerInfo> handlerInfos, INamedTypeSymbol unitSymbol, HashSet<string> namespaces)
        {
            var sb = new StringBuilder();
            // --- File Header ---
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            // --- Using Statements ---
            foreach (var ns in namespaces.OrderBy(n => n)) sb.AppendLine($"using {ns};");
            sb.AppendLine();

            // --- Namespace and Class Definition ---
            sb.AppendLine("namespace SourceMediator.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    internal static class GeneratedMediatorDispatcher");
            sb.AppendLine("    {");

            // --- Dictionaries ---
            sb.AppendLine($"        private static readonly Dictionary<Type, Func<object, IServiceProvider, CancellationToken, Task>> s_commandDispatchers = new({handlerInfos.Count});");
            sb.AppendLine($"        private static readonly Dictionary<Type, Func<object, IServiceProvider, CancellationToken, Task<object>>> s_requestDispatchers = new({handlerInfos.Count});");
            sb.AppendLine();

            // --- Static Constructor ---
            sb.AppendLine("        static GeneratedMediatorDispatcher()");
            sb.AppendLine("        {");

            // --- Populate Dictionaries ---
            foreach (var info in handlerInfos)
            {
                string hFullName = info.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string rFullName = info.RequestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string iFullName = info.InterfaceFullName;
                bool isCmd = info.ResponseType.Equals(unitSymbol, SymbolEqualityComparer.Default);

                sb.AppendLine($"            // Handler: {hFullName} Request: {rFullName}");
                if (isCmd)
                {
                    sb.AppendLine($"            s_commandDispatchers[typeof({rFullName})] = async (req, sp, ct) =>");
                    sb.AppendLine($"            {{ var handler = ({iFullName})sp.GetRequiredService<{hFullName}>(); await handler.Handle(({rFullName})req, ct); }};");
                }
                else
                {
                    sb.AppendLine($"            s_requestDispatchers[typeof({rFullName})] = async (req, sp, ct) =>");
                    sb.AppendLine($"            {{ var handler = ({iFullName})sp.GetRequiredService<{hFullName}>(); var result = await handler.Handle(({rFullName})req, ct); return (object)result!; }};");
                }
                sb.AppendLine();
            }
            sb.AppendLine("        }"); // End static constructor
            sb.AppendLine();

            // --- Dispatch Methods ---
            GenerateDispatchMethod(sb, isCommand: true, unitSymbol);
            GenerateDispatchMethod(sb, isCommand: false, unitSymbol);

            sb.AppendLine("    }"); // End class
            sb.AppendLine("}"); // End namespace
            return sb.ToString();
        }

        /// <summary>
        /// Generates the implementation class for the internal IGeneratedDispatcher interface.
        /// </summary>
        private static string GenerateDispatcherImplementation(INamedTypeSymbol generatedDispatcherInterfaceSymbol)
        {
            var sb = new StringBuilder();
            string interfaceFullName = generatedDispatcherInterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string interfaceNamespace = generatedDispatcherInterfaceSymbol.ContainingNamespace.ToDisplayString();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine($"using {interfaceNamespace}; // Namespace of IGeneratedDispatcher");
            sb.AppendLine();
            sb.AppendLine("namespace SourceMediator.Generated"); // Keep in Generated namespace
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Implements the internal dispatcher interface by delegating to the static dispatcher.</summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine($"    internal sealed class GeneratedDispatcher : {interfaceFullName}");
            sb.AppendLine("    {");
            sb.AppendLine("        public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)");
            sb.AppendLine("            => GeneratedMediatorDispatcher.DispatchRequestAsync<TResponse>(request, sp, ct);");
            sb.AppendLine();
            sb.AppendLine("        public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct)");
            sb.AppendLine("            => GeneratedMediatorDispatcher.DispatchCommandAsync(command, sp, ct);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the DI extension method for registering the IGeneratedDispatcher implementation.
        /// </summary>
        private static string GenerateDIRegistration(INamedTypeSymbol generatedDispatcherInterfaceSymbol)
        {
            var sb = new StringBuilder();
            string interfaceFullName = generatedDispatcherInterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string implementationFullName = "SourceMediator.Generated.GeneratedDispatcher"; // Full name of the implementation class
            string interfaceNamespace = generatedDispatcherInterfaceSymbol.ContainingNamespace.ToDisplayString();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine($"using {interfaceNamespace}; // Namespace of IGeneratedDispatcher");
            sb.AppendLine($"using SourceMediator.Generated; // Namespace of GeneratedDispatcher implementation");
            sb.AppendLine();
            // Put extensions in a relevant namespace, e.g., Microsoft.Extensions.DependencyInjection for discoverability
            sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Extension methods for registering generated SourceMediator components.</summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]"); // Hide from intellisense? Optional
            sb.AppendLine("    public static class SourceMediatorGeneratedDIExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Registers the generated dispatcher implementation.</summary>");
            sb.AppendLine("        /// <remarks>Call this after registering handlers and before resolving ISourceSender.</remarks>");
            sb.AppendLine("        public static IServiceCollection AddSourceMediatorGeneratedDispatcher(this IServiceCollection services)");
            sb.AppendLine("        {");
            // Use TryAddSingleton for the internal dispatcher interface implementation
            sb.AppendLine($"            services.TryAddSingleton<{interfaceFullName}, {implementationFullName}>();");
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
        // **** NEW HELPER METHOD ****
        /// <summary>
        /// Generates the DI extension method for registering all discovered concrete handlers.
        /// </summary>
        private static string GenerateHandlerRegistrationMethod(List<HandlerInfo> handlerInfos, HashSet<string> namespaces)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            // Add using statements for handler namespaces collected previously
            foreach (var ns in namespaces.Where(n => !n.StartsWith("System") && !n.StartsWith("Microsoft") && !n.StartsWith("SourceMediator")) // Avoid redundant usings
                                        .OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();
            // Place in a common namespace for extensions
            sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Extension methods for registering SourceMediator handlers discovered by the generator.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine("    public static class SourceMediatorGeneratedHandlersDIExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Registers all concrete Command and Query handlers discovered by the SourceMediator.Generator.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static IServiceCollection AddSourceMediatorHandlers(this IServiceCollection services)");
            sb.AppendLine("        {");

            // Loop through all discovered handlers and add registration lines
            foreach (var info in handlerInfos)
            {
                string handlerFullName = info.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                // Register the concrete handler type. Transient lifetime is typical for handlers.
                sb.AppendLine($"            services.TryAddTransient<{handlerFullName}>();");

                // --- Optional: Registering against specific interfaces ---
                // You might also want to register the handler against its specific closed-generic interface
                // although resolving the concrete type directly is often sufficient if dependencies ask for the concrete type.
                // If you need to resolve handlers via their specific interface (e.g., IEnumerable<IQueryHandler<...>>), uncomment below.
                // string interfaceFullName = info.InterfaceFullName; 
                // sb.AppendLine($"            services.TryAddTransient<{interfaceFullName}, {handlerFullName}>();");
                // --- End Optional ---
            }

            sb.AppendLine();
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
        /// <summary>
        /// Helper method to generate the body of the static dispatch methods.
        /// </summary>
        private static void GenerateDispatchMethod(StringBuilder sb, bool isCommand, INamedTypeSymbol unitSymbol)
        {
            string dictionaryName = isCommand ? "s_commandDispatchers" : "s_requestDispatchers";
            string inputParamName = isCommand ? "command" : "request";
            string returnType = isCommand ? "Task" : "Task<TResponse>";
            string methodName = isCommand ? "DispatchCommandAsync" : "DispatchRequestAsync";
            string genericParam = isCommand ? "" : "<TResponse>";
            string exceptionInterfaceText = isCommand ? "ICommandHandler<...>" : "IQueryHandler<...> or ICommandHandler<..., TResponse>";
            string taskResultVar = "result";
            string awaitPrefix = isCommand ? "" : "async ";
            string inputParameterType = "object";
            string unitFullName = unitSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string localRequestVarName = isCommand ? "commandType" : "requestType";

            sb.AppendLine($"        /// <summary>Dispatches a {(isCommand ? "command (Task return)" : "request (TResponse return)")}.</summary>");
            sb.AppendLine($"        public static {awaitPrefix}{returnType} {methodName}{genericParam}({inputParameterType} {inputParamName}, IServiceProvider serviceProvider, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var {localRequestVarName} = {inputParamName}.GetType();"); // Get runtime type
            sb.AppendLine();
            sb.AppendLine($"            // Try finding the handler in the primary dictionary");
            sb.AppendLine($"            if ({dictionaryName}.TryGetValue({localRequestVarName}, out var handlerFunc))");
            sb.AppendLine("            {");
            if (isCommand)
            { sb.AppendLine($"                return handlerFunc({inputParamName}, serviceProvider, cancellationToken);"); }
            else
            {
                sb.AppendLine($"                var {taskResultVar} = await handlerFunc({inputParamName}, serviceProvider, cancellationToken);");
                sb.AppendLine($"                // Check for null return from handler when TResponse is a non-nullable value type");
                sb.AppendLine($"                if ({taskResultVar} == null && default(TResponse) != null)");
                sb.AppendLine("                {");
                sb.AppendLine($"                   throw new InvalidCastException($\"[SourceMediator] Handler for {{{localRequestVarName}.FullName}} returned null, but expected non-nullable {{typeof(TResponse).FullName}}.\");");
                sb.AppendLine("                }");
                sb.AppendLine($"                return (TResponse){taskResultVar}!;"); // Cast/unbox result
            }
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine($"            // Fallback logic if not found in the primary dictionary");
            if (isCommand)
            {
                // If DispatchCommandAsync called for something that returns Unit but was registered as Request
                sb.AppendLine($"            if (s_requestDispatchers.TryGetValue({localRequestVarName}, out var reqFunc))");
                sb.AppendLine("            {");
                sb.AppendLine($"                 // Found in request dispatcher (likely returns Unit); execute and return Task.");
                sb.AppendLine($"                 return reqFunc({inputParamName}, serviceProvider, cancellationToken); // Return Task<object> as Task");
                sb.AppendLine("            }");
            }
            else
            {
                // If DispatchRequestAsync<Unit> called for something registered as Command
                sb.AppendLine($"            if (typeof(TResponse) == typeof({unitFullName}) && s_commandDispatchers.TryGetValue({localRequestVarName}, out var cmdFunc))");
                sb.AppendLine("            {");
                sb.AppendLine($"                // Found in command dispatcher and expecting Unit; execute and return Unit.");
                sb.AppendLine($"                await cmdFunc({inputParamName}, serviceProvider, cancellationToken);");
                sb.AppendLine($"                return (TResponse)(object){unitFullName}.Value; // Box Unit and cast to TResponse (Unit)");
                sb.AppendLine("            }");
            }
            sb.AppendLine();
            sb.AppendLine($"            // No handler found after primary lookup and fallback.");
            // Generate the exception message string carefully
            sb.Append($"            throw new InvalidOperationException($\"[SourceMediator] No handler registered for {{{localRequestVarName}.FullName}}");
            if (!isCommand) { sb.Append($" returning {{typeof(TResponse).FullName}}"); }
            sb.Append($". Ensure the handler class is concrete, public, implements {exceptionInterfaceText}, and is registered in the dependency injection container.\");");
            sb.AppendLine(); // Add the semicolon and newline

            sb.AppendLine("        }"); // End method
            sb.AppendLine();
        }

        /// <summary>
        /// Recursively collects namespaces from type symbols for 'using' statements.
        /// </summary>
        private static void CollectNamespaces(ITypeSymbol typeSymbol, HashSet<string> namespaces)
        {
            while (typeSymbol != null)
            {
                if (typeSymbol is INamedTypeSymbol namedType)
                {
                    // Add namespace of the type itself
                    if (namedType.ContainingNamespace != null && !namedType.ContainingNamespace.IsGlobalNamespace)
                    {
                        namespaces.Add(namedType.ContainingNamespace.ToDisplayString());
                    }
                    // Recurse for generic arguments
                    foreach (var arg in namedType.TypeArguments) CollectNamespaces(arg, namespaces);
                    // Move to containing type for next iteration (handles nested types)
                    typeSymbol = namedType.ContainingType;
                }
                else if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    // Process the element type of the array
                    typeSymbol = arrayType.ElementType;
                }
                else
                {
                    // Attempt to add namespace for other types and stop iterating this branch
                    if (typeSymbol.ContainingNamespace != null && !typeSymbol.ContainingNamespace.IsGlobalNamespace)
                    { namespaces.Add(typeSymbol.ContainingNamespace.ToDisplayString()); }
                    break;
                }
            }
        }
    } // End Generator class
} // End namespace
// NOTE: Ensure the IsExternalInit class below is defined if targeting .NET Standard 2.0
// Put this in a separate file like IsExternalInit.cs or at the bottom here.

namespace System.Runtime.CompilerServices
{
    [ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}