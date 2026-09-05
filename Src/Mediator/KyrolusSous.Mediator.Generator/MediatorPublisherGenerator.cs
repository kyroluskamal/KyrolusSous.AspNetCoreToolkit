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
        private const string NotificationHandlerInterfaceFullName = "KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusNotificationHandler`1";

        // Lives in the runtime package. Its presence decides whether the dispatch table can be
        // emitted at all: a project referencing only the abstractions has nothing to hand it to.
        private const string DispatchSourceInterfaceFullName = "KyrolusSous.Mediator.Runtime.GeneratorIntegration.IKyrolusNotificationDispatchSource";
        private const string DispatchSourceInterfaceQualifiedName = "global::" + DispatchSourceInterfaceFullName;
        private const string DispatchSourceImplQualifiedName = "global::KyrolusSous.Mediator.Generated.GeneratedNotificationDispatchSource";
        private const string NotificationInterfaceQualifiedName = "global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusNotification";
        private const string NotificationHandlerQualifiedName = "global::KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusNotificationHandler";

        // For SMG006 (ComputeCandidateOrphanNotifications): the universal notification marker, and
        // the interface every PublishAsync/Publish method - native or MediatR-compat - is ultimately
        // declared (or, for the compat extension methods, receives its `this` parameter) on. Mirrors
        // RequestBaseInterfaceFullName/MediatorSenderInterfaceFullName in MediatorImplementationGenerator.cs,
        // duplicated here rather than shared - the same "duplicated or moved to a shared location"
        // choice already made for the helpers below.
        private const string NotificationBaseInterfaceFullName = "KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusNotification";
        private const string MediatorPublisherInterfaceFullName = "KyrolusSous.Mediator.Abstractions.Interfaces.IKyrolusMediatorPublisher";

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

            // Every notification type this project both declares and publishes through the mediator -
            // candidates for SMG006 (see ComputeCandidateOrphanNotifications). Whether each one
            // actually has a handler is decided in Execute, against the handler infos collected there,
            // which this step has no access to; this step only answers "declared here and published
            // here". Off the raw Compilation, not the per-class pipeline above, for the same reason as
            // MediatorImplementationGenerator.cs's own ComputeCandidateOrphanRequests: correlating
            // declarations against PublishAsync call sites needs live symbols across the whole
            // compilation at once, which the per-class stage never sees together.
            IncrementalValueProvider<ImmutableArray<CandidateOrphanNotification>> candidateOrphanNotifications
                = context.CompilationProvider.Select(static (compilation, ct) => ComputeCandidateOrphanNotifications(compilation, ct));

            // Register the execution function to generate DI code
            context.RegisterSourceOutput(
                compilationAndNotificationHandlers.Combine(candidateOrphanNotifications),
                static (spc, source) => Execute(spc, source.Left.Compilation, source.Left.HandlerSymbols, source.Right));
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
        private static void Execute(
            SourceProductionContext context,
            Compilation compilation,
            ImmutableArray<INamedTypeSymbol> potentialHandlerSymbols,
            ImmutableArray<CandidateOrphanNotification> candidateOrphanNotifications)
        {
            // --- Get required INotificationHandler<> definition symbol ---
            INamedTypeSymbol? notificationHandlerDef = compilation.GetTypeByMetadataName(NotificationHandlerInterfaceFullName);

            if (notificationHandlerDef == null)
            {
                // Report diagnostic if IKyrolusNotificationHandler<> definition not found
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("SMG002", "IKyrolusNotificationHandler<> not found", "Could not find required KyrolusMediator interface IKyrolusNotificationHandler<>. Ensure KyrolusSous.Mediator.Abstractions.Interfaces assembly is referenced.", "KyrolusSous.Mediator.Generator", DiagnosticSeverity.Warning, true), // Changed to Warning as maybe no notifications are used
                    Location.None));
                return; // Don't generate if the base interface isn't found
            }

            // --- Analyze and collect valid notification handlers ---
            var notificationHandlerInfos = new List<NotificationHandlerInfo>();
            var openGenericHandlerInfos = new List<OpenGenericNotificationHandlerInfo>();
            // Collect namespaces needed for the generated DI registration file
            var namespaces = new HashSet<string> { "System", "System.Collections.Generic", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Extensions", "KyrolusSous.Mediator.Abstractions.Interfaces" };

            // Populated even when there are no handlers at all - the same reasoning as
            // MediatorImplementationGenerator.cs's Execute: that is exactly the case SMG006 below
            // most needs to catch (a project that publishes notifications but implements no handlers
            // for any of them), and the generation guarded further down still checks
            // notificationHandlerInfos.Count > 0 on its own.
            if (!potentialHandlerSymbols.IsDefaultOrEmpty)
            {
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
                    foreach (var notifInfo in GetNotificationHandlerInfos(handlerSymbol, notificationHandlerDef))
                    {
                        notificationHandlerInfos.Add(notifInfo);
                        // Collect namespaces from handler and notification types
                        CollectNamespaces(notifInfo.HandlerType, namespaces);
                        CollectNamespaces(notifInfo.NotificationType, namespaces);
                    }
                }
            }

            ReportOrphanNotifications(context, notificationHandlerInfos, openGenericHandlerInfos, candidateOrphanNotifications);

            // The dispatch table calls into the runtime package; without it there is nothing to
            // implement and the file would not compile.
            var runtimeAvailable = compilation.GetTypeByMetadataName(DispatchSourceInterfaceFullName) is not null;

            // --- Generate DI registration code if handlers were found ---
            if (notificationHandlerInfos.Count > 0 || openGenericHandlerInfos.Count > 0)
            {
                string diExtensionCode = GenerateNotificationHandlerRegistrationMethod(
                    notificationHandlerInfos, openGenericHandlerInfos, namespaces, runtimeAvailable);
                // Use a distinct file name for this generator's output
                context.AddSource("KyrolusSous.Mediator.GeneratedNotificationHandlersDI.g.cs", SourceText.From(diExtensionCode, Encoding.UTF8));
            }

            // The publisher otherwise reaches every handler through MakeGenericType and
            // MethodInfo.Invoke, neither of which survives NativeAOT.
            if (runtimeAvailable && notificationHandlerInfos.Count > 0)
            {
                string dispatchSourceCode = GenerateNotificationDispatchSource(notificationHandlerInfos);
                context.AddSource("KyrolusSous.Mediator.GeneratedNotificationDispatch.g.cs", SourceText.From(dispatchSourceCode, Encoding.UTF8));
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

        /// <summary>
        /// Yields one <see cref="NotificationHandlerInfo"/> per notification the class handles.
        /// </summary>
        /// <remarks>
        /// Every match is returned, not just the first. A class subscribing to several
        /// notifications - <c>class Auditor : INotificationHandler&lt;UserCreated&gt;,
        /// INotificationHandler&lt;UserDeleted&gt;</c> - is ordinary, and returning after the first
        /// left the rest with no registration at all, so publishing them reached this handler for
        /// one notification and silently skipped it for the others.
        /// </remarks>
        private static IEnumerable<NotificationHandlerInfo> GetNotificationHandlerInfos(
            INamedTypeSymbol handlerSymbol,
            INamedTypeSymbol notificationHandlerDefinition)
        {
            // INotificationHandler<in TNotification> is contravariant, so AllInterfaces can report
            // the same notification through more than one route. A repeated entry would emit a
            // duplicate registration and a duplicate dictionary key.
            var emitted = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            foreach (var iface in handlerSymbol.AllInterfaces)
            {
                if (!iface.IsGenericType || iface.TypeArguments.Length != 1) continue;
                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, notificationHandlerDefinition)) continue;

                if (iface.TypeArguments[0] is not INamedTypeSymbol notificationType) continue;
                if (!emitted.Add(notificationType)) continue;

                // Get the specific constructed interface name like INotificationHandler<MyNotification>
                var interfaceFullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                yield return new NotificationHandlerInfo(handlerSymbol, notificationType, interfaceFullName);
            }
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

        /// <summary>One notification type SMG006 should consider: declared and published in this project.</summary>
        private sealed record CandidateOrphanNotification(string NotificationFullName, Location Location);

        /// <summary>
        /// Finds every notification type this project both declares and publishes through the
        /// mediator - candidates for SMG006. Whether each candidate actually has a handler is decided
        /// later in <see cref="ReportOrphanNotifications"/>, against data (handler infos, open generic
        /// handler infos) this method has no access to.
        /// </summary>
        /// <remarks>
        /// Deliberately scoped to "declared here AND published here", mirroring
        /// <c>ComputeCandidateOrphanRequests</c> in <c>MediatorImplementationGenerator.cs</c> exactly:
        /// a notification declared in a shared contracts assembly and handled in a separate,
        /// unreferenced handlers assembly is a legitimate, common split this method cannot see across
        /// - the handler is real, just not visible to this compilation - so only asking "does this
        /// project, entirely on its own, actually work" avoids flagging that split as a mistake.
        /// </remarks>
        private static ImmutableArray<CandidateOrphanNotification> ComputeCandidateOrphanNotifications(Compilation compilation, CancellationToken cancellationToken)
        {
            var notificationBaseDef = compilation.GetTypeByMetadataName(NotificationBaseInterfaceFullName);
            var publisherDef = compilation.GetTypeByMetadataName(MediatorPublisherInterfaceFullName);
            if (notificationBaseDef is null || publisherDef is null) return ImmutableArray<CandidateOrphanNotification>.Empty;

            // 1. Every concrete notification type this project declares, keyed by symbol so the
            //    "published here" pass below can look each one up by identity rather than by name.
            var declared = new Dictionary<INamedTypeSymbol, Location>(SymbolEqualityComparer.Default);

            foreach (var tree in compilation.SyntaxTrees)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var semanticModel = compilation.GetSemanticModel(tree);

                // Notifications are declared `record Foo(...) : IKyrolusNotification;` at least as
                // often as `class Foo`, so both shapes are scanned here.
                foreach (var typeDeclaration in tree.GetRoot(cancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not INamedTypeSymbol symbol) continue;
                    if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct)) continue;
                    if (symbol.IsAbstract || symbol.IsStatic || symbol.IsGenericType) continue;
                    if (!ImplementsInterface(symbol, notificationBaseDef)) continue;

                    declared[symbol] = typeDeclaration.Identifier.GetLocation();
                }
            }

            if (declared.Count == 0) return ImmutableArray<CandidateOrphanNotification>.Empty;

            // 2. Every notification type actually published through the mediator in this same
            //    project - the other half of "declared here and published here".
            var published = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var tree in compilation.SyntaxTrees)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var semanticModel = compilation.GetSemanticModel(tree);

                foreach (var invocation in tree.GetRoot(cancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) continue;
                    if (memberAccess.Name.Identifier.Text is not ("PublishAsync" or "Publish")) continue;
                    if (invocation.ArgumentList.Arguments.Count == 0) continue;

                    if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol) continue;

                    // The receiver must implement IKyrolusMediatorPublisher - the interface every one
                    // of these methods (native or MediatR-compat) is ultimately declared on, or takes
                    // as its `this` parameter - or this is an unrelated method that merely happens to
                    // share a name.
                    ITypeSymbol? receiverType;
                    if (methodSymbol.IsExtensionMethod)
                    {
                        var original = methodSymbol.ReducedFrom ?? methodSymbol;
                        if (original.Parameters.Length == 0) continue;
                        receiverType = original.Parameters[0].Type;
                    }
                    else
                    {
                        receiverType = methodSymbol.ContainingType;
                    }

                    if (receiverType is null || !ImplementsInterface(receiverType, publisherDef)) continue;

                    // The static type of the argument expression, e.g. `SomethingHappened` for
                    // `publisher.PublishAsync(new SomethingHappened())`. Left alone (not added to
                    // `published`) when it resolves to an interface or an abstract type -
                    // `publisher.PublishAsync(notification)` for some `IKyrolusNotification notification`
                    // names no specific type, so there is nothing concrete to hold this project to.
                    if (semanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).Type is not INamedTypeSymbol argumentType) continue;
                    if (argumentType.IsAbstract) continue;

                    published.Add(argumentType);
                }
            }

            if (published.Count == 0) return ImmutableArray<CandidateOrphanNotification>.Empty;

            // 3. The intersection.
            var candidates = ImmutableArray.CreateBuilder<CandidateOrphanNotification>();
            foreach (var pair in declared)
            {
                if (!published.Contains(pair.Key)) continue;
                candidates.Add(new CandidateOrphanNotification(
                    pair.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    pair.Value));
            }

            return candidates.ToImmutable();
        }

        /// <summary>
        /// Reports SMG006 for every candidate <see cref="ComputeCandidateOrphanNotifications"/> found -
        /// a notification this project both declares and publishes through the mediator - that this
        /// project has no handler for.
        /// </summary>
        /// <remarks>
        /// A warning, not an error, for the same reason SMG005 is one rather than an error: a
        /// notification declared here can genuinely be handled in a different project this compilation
        /// does not reference, or only against the MediatR-compat <c>INotificationHandler&lt;&gt;</c>
        /// interface without going through generation - both legitimate, both invisible from here.
        /// </remarks>
        private static void ReportOrphanNotifications(
            SourceProductionContext context,
            List<NotificationHandlerInfo> notificationHandlerInfos,
            List<OpenGenericNotificationHandlerInfo> openGenericHandlerInfos,
            ImmutableArray<CandidateOrphanNotification> candidateOrphanNotifications)
        {
            if (candidateOrphanNotifications.IsDefaultOrEmpty) return;

            // An open generic handler might cover any notification shape, and confirming whether it
            // actually covers this one needs the same constraint-satisfaction check ReportOrphanRequests'
            // own analogous guard avoids paying for just to decide whether to print a warning. Staying
            // silent whenever one exists trades a handful of missed warnings for zero false positives
            // from this specific cause.
            if (openGenericHandlerInfos.Count > 0) return;

            var handledNotificationNames = new HashSet<string>(
                notificationHandlerInfos.Select(info => info.NotificationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                StringComparer.Ordinal);

            var descriptor = new DiagnosticDescriptor(
                id: "SMG006",
                title: "Notification has no handler in this project",
                messageFormat: "'{0}' is published through the mediator in this project, but the source generator found no handler for it in this project. If you have confirmed a handler for it is registered from a different project, or only against the MediatR-compat INotificationHandler<> interface, this is a false positive and safe to ignore - suppress it with '#pragma warning disable SMG006' around the declaration, or 'dotnet_diagnostic.SMG006.severity = none' in .editorconfig. Otherwise, publishing it will silently do nothing at runtime.",
                category: "KyrolusSous.Mediator.Generator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "SMG006 only ever looks at this one project: it cannot see a handler registered from a different project the same solution builds, or one registered only against the MediatR-compat INotificationHandler<> interface without going through generation - both are legitimate reasons to suppress it rather than a bug to fix.");

            foreach (var candidate in candidateOrphanNotifications)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (handledNotificationNames.Contains(candidate.NotificationFullName)) continue;

                context.ReportDiagnostic(Diagnostic.Create(descriptor, candidate.Location, candidate.NotificationFullName));
            }
        }

        /// <summary>Whether <paramref name="type"/> is, or implements, <paramref name="interfaceDef"/>.</summary>
        private static bool ImplementsInterface(ITypeSymbol type, INamedTypeSymbol interfaceDef)
        {
            if (SymbolEqualityComparer.Default.Equals(type, interfaceDef)) return true;

            foreach (var iface in type.AllInterfaces)
                if (SymbolEqualityComparer.Default.Equals(iface, interfaceDef))
                    return true;

            return false;
        }

        // Generates the DI extension method for registering notification handlers
        private static string GenerateNotificationHandlerRegistrationMethod(
            List<NotificationHandlerInfo> notificationHandlerInfos,
            List<OpenGenericNotificationHandlerInfo> openGenericHandlerInfos,
            HashSet<string> namespaces,
            bool runtimeAvailable)
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
            if (runtimeAvailable && notificationHandlerInfos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("            // Lets the publisher call each handler directly instead of closing");
                sb.AppendLine("            // INotificationHandler<> with MakeGenericType, which NativeAOT cannot do.");
                sb.AppendLine($"            services.TryAddSingleton<{DispatchSourceInterfaceQualifiedName}, {DispatchSourceImplQualifiedName}>();");
            }
            sb.AppendLine();
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the notification dispatch table: one entry per notification type, each binding
        /// its handlers through ordinary generic calls rather than reflection.
        /// </summary>
        /// <remarks>
        /// <c>Bind&lt;TNotification&gt;</c> is written once and instantiated per notification by the
        /// table below. Because every instantiation appears as a concrete static call, the compiler
        /// can emit them ahead of time - which is exactly what <c>MakeGenericType</c> plus
        /// <c>MethodInfo.Invoke</c> made impossible.
        /// </remarks>
        private static string GenerateNotificationDispatchSource(List<NotificationHandlerInfo> notificationHandlerInfos)
        {
            // One entry per notification, not per handler: several handlers for the same
            // notification share a single table entry, and GetServices returns all of them.
            var notificationTypes = notificationHandlerInfos
                .Select(info => info.NotificationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine("namespace KyrolusSous.Mediator.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Notification handler calls bound at compile time, so none is found by reflection.</summary>");
            sb.AppendLine("    [System.Runtime.CompilerServices.CompilerGenerated]");
            sb.AppendLine($"    internal sealed class GeneratedNotificationDispatchSource : {DispatchSourceInterfaceQualifiedName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private static readonly Dictionary<Type, Func<object, IServiceProvider, IReadOnlyList<Func<CancellationToken, Task>>>> s_dispatchers = new({notificationTypes.Count})");
            sb.AppendLine("        {");
            foreach (var notificationType in notificationTypes)
            {
                sb.AppendLine($"            [typeof({notificationType})] = static (notification, serviceProvider) => Bind<{notificationType}>(notification, serviceProvider),");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Resolves the handlers for one notification and binds each to a call.</summary>");
            sb.AppendLine("        private static IReadOnlyList<Func<CancellationToken, Task>> Bind<TNotification>(object notification, IServiceProvider serviceProvider)");
            sb.AppendLine($"            where TNotification : {NotificationInterfaceQualifiedName}");
            sb.AppendLine("        {");
            sb.AppendLine("            var typed = (TNotification)notification;");
            sb.AppendLine("            var invocations = new List<Func<CancellationToken, Task>>();");
            sb.AppendLine($"            foreach (var handler in serviceProvider.GetServices<{NotificationHandlerQualifiedName}<TNotification>>())");
            sb.AppendLine("            {");
            sb.AppendLine("                invocations.Add(cancellationToken => handler.Handle(typed, cancellationToken));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return invocations;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public IReadOnlyList<Func<CancellationToken, Task>>? CreateHandlerInvocations(object notification, IServiceProvider serviceProvider)");
            sb.AppendLine("            => s_dispatchers.TryGetValue(notification.GetType(), out var bind) ? bind(notification, serviceProvider) : null;");
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
