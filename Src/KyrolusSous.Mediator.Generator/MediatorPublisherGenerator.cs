// MediatorPublisherGenerator.cs (NEW FILE in KyrolusSous.Mediator.Generator project)
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

// Using the same namespace as the other generator for simplicity, or use a distinct one
namespace KyrolusSous.Mediator.Generator
{
    /// <summary>
    /// Source Generator responsible *only* for discovering and generating
    /// Dependency Injection registration code for <c>INotificationHandler&lt;&gt;</c> implementations.
    /// </summary>
    [Generator] // Mark as a generator
    public class MediatorPublisherGenerator : IIncrementalGenerator
    {
        // --- Constants ---
        private const string NotificationHandlerInterfaceFullName = "KyrolusSous.Mediator.Abstractions.Interfaces.INotificationHandler`1";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Pipeline to find concrete classes that might be notification handlers
            IncrementalValuesProvider<INamedTypeSymbol> notificationHandlerSymbols = context.SyntaxProvider
                        .CreateSyntaxProvider(
                            // Modify the predicate here:
                            predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null }, // Removed AttributeLists check
                            transform: static (ctx, ct) => GetSemanticTargetForGeneration(ctx, ct))
                        .Where(static symbol => symbol is not null)!;

            // Combine with compilation (needed to resolve the INotificationHandler<> definition)
            IncrementalValueProvider<(Compilation Compilation, ImmutableArray<INamedTypeSymbol> HandlerSymbols)> compilationAndNotificationHandlers
                = context.CompilationProvider.Combine(notificationHandlerSymbols.Collect());

            // Register the execution function to generate DI code
            context.RegisterSourceOutput(compilationAndNotificationHandlers, Execute);
        }

        // Semantic filter: Check if the syntax represents a concrete (non-abstract, non-static) class symbol
        // (This helper can be duplicated from MediatorGenerator.cs or moved to a shared file)
        private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken) as INamedTypeSymbol;

            if (classSymbol == null || classSymbol.IsAbstract || classSymbol.IsStatic)
            {
                return null;
            }
            return classSymbol;
        }

        // Execution method for this generator
        private static void Execute(SourceProductionContext context, (Compilation Compilation, ImmutableArray<INamedTypeSymbol> HandlerSymbols) source)
        {
            var (compilation, potentialHandlerSymbols) = source;
            if (potentialHandlerSymbols.IsDefaultOrEmpty) return;

            // --- Get required INotificationHandler<> definition symbol ---
            INamedTypeSymbol? notificationHandlerDef = compilation.GetTypeByMetadataName(NotificationHandlerInterfaceFullName);

            if (notificationHandlerDef == null)
            {
                // Report diagnostic if INotificationHandler<> definition not found
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("SMG002", "INotificationHandler<> not found", "Could not find required KyrolusMediator interface INotificationHandler<>. Ensure KyrolusSous.Mediator.Abstractions.Interfaces assembly is referenced.", "KyrolusSous.Mediator.Generator", DiagnosticSeverity.Warning, true), // Changed to Warning as maybe no notifications are used
                    Location.None));
                return; // Don't generate if the base interface isn't found
            }

            // --- Analyze and collect valid notification handlers ---
            var notificationHandlerInfos = new List<NotificationHandlerInfo>();
            var openGenericHandlerInfos = new List<OpenGenericNotificationHandlerInfo>();
            // Collect namespaces needed for the generated DI registration file
            var namespaces = new HashSet<string> { "System", "System.Collections.Generic", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Extensions", "KyrolusSous.Mediator.Abstractions.Interfaces" };

            foreach (var handlerSymbol in potentialHandlerSymbols)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (handlerSymbol.IsGenericType && handlerSymbol.TypeParameters.Length > 0)
                {
                    foreach (var openGeneric in TryGetOpenGenericNotificationHandlerInfos(handlerSymbol, notificationHandlerDef))
                    {
                        openGenericHandlerInfos.Add(openGeneric);
                    }
                    continue;
                }
                // Use the specific helper for notification handlers
                var notifInfo = TryGetNotificationHandlerInfo(handlerSymbol, notificationHandlerDef);
                if (notifInfo != null)
                {
                    notificationHandlerInfos.Add(notifInfo);
                    // Collect namespaces from handler and notification types
                    CollectNamespaces(notifInfo.HandlerType, namespaces);
                    CollectNamespaces(notifInfo.NotificationType, namespaces);
                }
            }

            // --- Generate DI registration code if handlers were found ---
            if (notificationHandlerInfos.Count > 0 || openGenericHandlerInfos.Count > 0)
            {
                string diExtensionCode = GenerateNotificationHandlerRegistrationMethod(notificationHandlerInfos, openGenericHandlerInfos, namespaces);
                // Use a distinct file name for this generator's output
                context.AddSource("KyrolusSous.Mediator.GeneratedNotificationHandlersDI.g.cs", SourceText.From(diExtensionCode, Encoding.UTF8));
            }
        }

        // --- Helper Class ---
        // (This can be duplicated or moved to a shared file)
        private sealed class NotificationHandlerInfo
        {
            public NotificationHandlerInfo(INamedTypeSymbol handlerType, INamedTypeSymbol notificationType, string interfaceFullName)
            {
                HandlerType = handlerType;
                NotificationType = notificationType;
                InterfaceFullName = interfaceFullName;
            }

            public INamedTypeSymbol HandlerType { get; }
            public INamedTypeSymbol NotificationType { get; }
            public string InterfaceFullName { get; }
        }

        private sealed class OpenGenericNotificationHandlerInfo
        {
            public OpenGenericNotificationHandlerInfo(INamedTypeSymbol handlerType, INamedTypeSymbol interfaceType)
            {
                HandlerType = handlerType;
                InterfaceType = interfaceType;
            }

            public INamedTypeSymbol HandlerType { get; }
            public INamedTypeSymbol InterfaceType { get; }
        }

        // --- Helper Methods (Duplicated or moved to a shared location) ---

        // Analyzes a class symbol to see if it implements INotificationHandler<>
        private static NotificationHandlerInfo? TryGetNotificationHandlerInfo(
            INamedTypeSymbol handlerSymbol,
            INamedTypeSymbol notificationHandlerDefinition)
        {
            foreach (var iface in handlerSymbol.AllInterfaces)
            {
                if (iface.IsGenericType && iface.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, notificationHandlerDefinition))
                {
                    var notificationType = iface.TypeArguments[0] as INamedTypeSymbol;
                    // Get the specific constructed interface name like INotificationHandler<MyNotification>
                    var interfaceFullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (notificationType != null)
                    {
                        return new NotificationHandlerInfo(handlerSymbol, notificationType, interfaceFullName);
                    }
                }
            }
            return null;
        }

        private static IEnumerable<OpenGenericNotificationHandlerInfo> TryGetOpenGenericNotificationHandlerInfos(
            INamedTypeSymbol handlerSymbol,
            INamedTypeSymbol notificationHandlerDefinition)
        {
            foreach (var iface in handlerSymbol.AllInterfaces)
            {
                if (!iface.IsGenericType || iface.TypeArguments.Length != 1) continue;
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, notificationHandlerDefinition))
                {
                    yield return new OpenGenericNotificationHandlerInfo(handlerSymbol.OriginalDefinition, iface.OriginalDefinition);
                }
            }
        }

        // Generates the DI extension method for registering notification handlers
        private static string GenerateNotificationHandlerRegistrationMethod(
            List<NotificationHandlerInfo> notificationHandlerInfos,
            List<OpenGenericNotificationHandlerInfo> openGenericHandlerInfos,
            HashSet<string> namespaces)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            // Written explicitly, then excluded from the loop below. `namespaces` is seeded with
            // these same values, so emitting both produced a duplicate `using` and CS0105 in every
            // consuming project.
            var alwaysEmitted = new HashSet<string>
            {
                "Microsoft.Extensions.DependencyInjection",
                "Microsoft.Extensions.DependencyInjection.Extensions",
                "KyrolusSous.Mediator.Abstractions.Interfaces"
            };

            foreach (var ns in alwaysEmitted.OrderBy(n => n, StringComparer.Ordinal))
            {
                sb.AppendLine($"using {ns};");
            }

            // Add unique using statements for handler/notification namespaces
            foreach (var ns in namespaces
                .Where(n => !alwaysEmitted.Contains(n))
                .Where(n => !n.StartsWith("System") && !n.StartsWith("Microsoft"))
                .OrderBy(n => n, StringComparer.Ordinal))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();
            sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection"); // Standard namespace for DI extensions
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Extension methods for registering KyrolusMediator Notification Handlers discovered by MediatorPublisherGenerator.</summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine("    public static class KyrolusMediatorGeneratedNotificationHandlersDIExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Registers all concrete Notification handlers discovered by the KyrolusSous.Mediator.Generator.</summary>");
            sb.AppendLine("        public static IServiceCollection AddKyrolusMediatorNotificationHandlers(this IServiceCollection services)");
            sb.AppendLine("        {");
            foreach (var info in notificationHandlerInfos)
            {
                string handlerFullName = info.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string interfaceFullName = info.InterfaceFullName; // The specific INotificationHandler<TNotification>

                sb.AppendLine($"            // Registering Handler: {handlerFullName} for Notification: {info.NotificationType.Name}");
                // Register the concrete handler AS AN IMPLEMENTATION of the specific interface using TryAddEnumerable
                sb.AppendLine($"            services.TryAddEnumerable(ServiceDescriptor.Transient<{interfaceFullName}, {handlerFullName}>());");
            }
            if (openGenericHandlerInfos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("            // Open generic notification handlers");
            }
            foreach (var info in openGenericHandlerInfos)
            {
                string interfaceType = GetOpenGenericTypeOf(info.InterfaceType);
                string handlerType = GetOpenGenericTypeOf(info.HandlerType);
                sb.AppendLine($"            services.TryAddEnumerable(ServiceDescriptor.Transient({interfaceType}, {handlerType}));");
            }
            sb.AppendLine();
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GetOpenGenericTypeOf(INamedTypeSymbol symbol)
        {
            string name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!symbol.IsGenericType)
            {
                return $"typeof({name})";
            }

            int openIndex = name.IndexOf('<');
            if (openIndex < 0)
            {
                return $"typeof({name})";
            }

            int arity = symbol.TypeParameters.Length;
            string commas = new string(',', Math.Max(0, arity - 1));
            return $"typeof({name.Substring(0, openIndex)}<{commas}>)";
        }

        // Helper to collect namespaces (Duplicated or moved to a shared location)
        private static void CollectNamespaces(ITypeSymbol typeSymbol, HashSet<string> namespaces)
        {
            if (typeSymbol == null)
                return;

            if (typeSymbol is IArrayTypeSymbol arrayType)
            {
                CollectNamespaces(arrayType.ElementType, namespaces);
                return;
            }

            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (namedType.ContainingNamespace != null && !namedType.ContainingNamespace.IsGlobalNamespace)
                {
                    namespaces.Add(namedType.ContainingNamespace.ToDisplayString());
                }
                foreach (var arg in namedType.TypeArguments)
                {
                    CollectNamespaces(arg, namespaces);
                }
                if (namedType.ContainingType != null)
                {
                    CollectNamespaces(namedType.ContainingType, namespaces);
                }
                return;
            }

            if (typeSymbol.ContainingNamespace != null && !typeSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                namespaces.Add(typeSymbol.ContainingNamespace.ToDisplayString());
            }
        }
    }
}
