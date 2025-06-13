// MediatorPublisherGenerator.cs (NEW FILE in SourceMediator.Generator project)
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
namespace KyrolusSous.SourceMediator.Generator
{
    /// <summary>
    /// Source Generator responsible *only* for discovering and generating
    /// Dependency Injection registration code for INotificationHandler<> implementations.
    /// </summary>
    [Generator] // Mark as a generator
    public class MediatorPublisherGenerator : IIncrementalGenerator
    {
        // --- Constants ---
        private const string NotificationHandlerInterfaceFullName = "SourceMediator.Interfaces.INotificationHandler`1";

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
                    new DiagnosticDescriptor("SMG002", "INotificationHandler<> not found", "Could not find required SourceMediator interface INotificationHandler<>. Ensure SourceMediator.Interfaces assembly is referenced.", "SourceMediator.Generator", DiagnosticSeverity.Warning, true), // Changed to Warning as maybe no notifications are used
                    Location.None));
                return; // Don't generate if the base interface isn't found
            }

            // --- Analyze and collect valid notification handlers ---
            var notificationHandlerInfos = new List<NotificationHandlerInfo>();
            // Collect namespaces needed for the generated DI registration file
            var namespaces = new HashSet<string> { "System", "System.Collections.Generic", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Extensions", "SourceMediator.Interfaces" };

            foreach (var handlerSymbol in potentialHandlerSymbols)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
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
            if (notificationHandlerInfos.Count > 0)
            {
                string diExtensionCode = GenerateNotificationHandlerRegistrationMethod(notificationHandlerInfos, namespaces);
                // Use a distinct file name for this generator's output
                context.AddSource("SourceMediator.GeneratedNotificationHandlersDI.g.cs", SourceText.From(diExtensionCode, Encoding.UTF8));
            }
        }

        // --- Helper Record ---
        // (This can be duplicated or moved to a shared file)
        private sealed record NotificationHandlerInfo(INamedTypeSymbol HandlerType, INamedTypeSymbol NotificationType, string InterfaceFullName);

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

        // Generates the DI extension method for registering notification handlers
        private static string GenerateNotificationHandlerRegistrationMethod(List<NotificationHandlerInfo> notificationHandlerInfos, HashSet<string> namespaces)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using SourceMediator.Interfaces; // For INotificationHandler<>");
            // Add unique using statements for handler/notification namespaces
            foreach (var ns in namespaces.Where(n => !n.StartsWith("System") && !n.StartsWith("Microsoft")).OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();
            sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection"); // Standard namespace for DI extensions
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Extension methods for registering SourceMediator Notification Handlers discovered by MediatorPublisherGenerator.</summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine("    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
            sb.AppendLine("    public static class SourceMediatorGeneratedNotificationHandlersDIExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Registers all concrete Notification handlers discovered by the SourceMediator.Generator.</summary>");
            sb.AppendLine("        public static IServiceCollection AddSourceMediatorNotificationHandlers(this IServiceCollection services)");
            sb.AppendLine("        {");
            foreach (var info in notificationHandlerInfos)
            {
                string handlerFullName = info.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string interfaceFullName = info.InterfaceFullName; // The specific INotificationHandler<TNotification>

                sb.AppendLine($"            // Registering Handler: {handlerFullName} for Notification: {info.NotificationType.Name}");
                // Register the concrete handler AS AN IMPLEMENTATION of the specific interface using TryAddEnumerable
                sb.AppendLine($"            services.TryAddEnumerable(ServiceDescriptor.Transient<{interfaceFullName}, {handlerFullName}>());");
            }
            sb.AppendLine();
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
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