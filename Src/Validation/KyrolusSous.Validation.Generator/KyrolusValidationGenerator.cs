using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KyrolusSous.Validation.Generator;

/// <summary>
/// Roslyn incremental source generator that auto-registers <em>every</em> <c>IKyrolusRequestValidator&lt;T&gt;</c>
/// implementation found in the compiling project into the DI container - regardless of how each one was written
/// (hand-written, Fluent DSL, FluentValidation adapter, or even the DataAnnotations source generator's own
/// output). This is a registration-only generator: it does not generate any validator logic itself, and is the
/// AOT-friendly alternative to <c>KyrolusSous.Validation.Runtime</c>'s reflection-based
/// <c>AddKyrolusValidationRuntimeScanning(...)</c>/<c>AddKyrolusScannedValidators(...)</c>.
/// </summary>
/// <remarks>
/// Emits a single <c>KyrolusValidationGeneratedServiceCollectionExtensions</c> class with:
/// <list type="bullet">
/// <item><description><c>AddKyrolusGeneratedValidators()</c> - registers every discovered validator, if any were found.</description></item>
/// <item><description><c>AddKyrolusGeneratedValidationProfiles()</c> - registers the four built-in
/// <c>KyrolusValidationProfiles</c> (Create, Update, UiHints, BackgroundJobs), emitted only when the compiling
/// project references <c>KyrolusSous.Validation.Abstractions</c> (so this generator itself has no hard
/// dependency on that package).</description></item>
/// <item><description><c>AddKyrolusGeneratedValidationHookOrder()</c> - registers a generated
/// <c>IKyrolusValidationHookOrderLookup</c> mapping each <c>IKyrolusValidationHook</c>/<c>IKyrolusValidationHook&lt;T&gt;</c>
/// implementation decorated with <c>[KyrolusValidationHookOrder(n)]</c> to its declared order, emitted only when
/// at least one such hook was found. This is the AOT-safe alternative to reading the attribute via runtime
/// reflection: the mapping is resolved once, here, at compile time.</description></item>
/// </list>
/// Nothing is emitted at all when none of these conditions apply, so referencing this generator in a project with
/// no validators or ordered hooks yet is harmless.
/// </remarks>
/// <example>
/// <code>
/// // Program.cs - no manual services.AddScoped&lt;IKyrolusRequestValidator&lt;T&gt;, ...&gt;() calls needed:
/// builder.Services.AddKyrolusValidationRuntime();
/// builder.Services.AddKyrolusGeneratedValidators();
/// builder.Services.AddKyrolusGeneratedValidationProfiles();
/// </code>
/// </example>
[Generator]
public sealed class KyrolusValidationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            static (ctx, _) => (INamedTypeSymbol?)ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node))
            .Where(static symbol => symbol is not null);

        var compilationAndCandidates = context.CompilationProvider.Combine(candidates.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, pair) =>
        {
            Emit(spc, pair.Left, pair.Right);
        });
    }

    /// <summary>
    /// Filters <paramref name="candidates"/> down to concrete, non-generic classes that close
    /// <c>IKyrolusRequestValidator&lt;T&gt;</c> (deduplicating (service type, implementation type) pairs via the
    /// <see cref="HashSet{T}"/>) and, separately, down to ones carrying <c>[KyrolusValidationHookOrder(n)]</c> that
    /// implement <c>IKyrolusValidationHook</c>/<c>IKyrolusValidationHook&lt;T&gt;</c>, then emits the extension
    /// class(es) described on <see cref="KyrolusValidationGenerator"/>.
    /// </summary>
    private static void Emit(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> candidates)
    {
        var validatorInterface = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.IKyrolusRequestValidator`1");
        if (validatorInterface is null)
        {
            return;
        }

        var profileType = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.KyrolusValidationProfile");
        var profilesType = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.KyrolusValidationProfiles");
        var emitProfiles = profileType is not null && profilesType is not null;

        var hookInterface = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.IKyrolusValidationHook");
        var hookGenericInterface = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.IKyrolusValidationHook`1");
        var hookOrderAttributeType = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.KyrolusValidationHookOrderAttribute");
        var hookOrderLookupType = compilation.GetTypeByMetadataName("KyrolusSous.Validation.Abstractions.IKyrolusValidationHookOrderLookup");
        var canEmitHookOrder = hookOrderAttributeType is not null && hookOrderLookupType is not null
            && (hookInterface is not null || hookGenericInterface is not null);

        var registrations = new HashSet<(string ServiceType, string ImplementationType)>();
        var hookOrders = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (candidate is null || candidate.IsAbstract || candidate.TypeParameters.Length > 0)
            {
                continue;
            }

            foreach (var iface in candidate.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, validatorInterface))
                {
                    continue;
                }

                var serviceType = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var implType = candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                registrations.Add((serviceType, implType));
            }

            if (!canEmitHookOrder)
            {
                continue;
            }

            var orderAttribute = candidate.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hookOrderAttributeType));
            if (orderAttribute is null
                || orderAttribute.ConstructorArguments.Length == 0
                || orderAttribute.ConstructorArguments[0].Value is not int order)
            {
                continue;
            }

            var implementsHook = candidate.AllInterfaces.Any(iface =>
                SymbolEqualityComparer.Default.Equals(iface, hookInterface)
                || SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, hookGenericInterface));
            if (!implementsHook)
            {
                continue;
            }

            hookOrders[candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)] = order;
        }

        var emitHookOrder = hookOrders.Count > 0;

        if (registrations.Count == 0 && !emitProfiles && !emitHookOrder)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (emitProfiles || emitHookOrder)
        {
            sb.AppendLine("using KyrolusSous.Validation.Abstractions;");
        }
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine();
        sb.AppendLine("namespace KyrolusSous.Validation.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class KyrolusValidationGeneratedServiceCollectionExtensions");
        sb.AppendLine("{");
        if (registrations.Count > 0)
        {
            sb.AppendLine("    public static IServiceCollection AddKyrolusGeneratedValidators(this IServiceCollection services)");
            sb.AppendLine("    {");

            foreach (var (ServiceType, ImplementationType) in registrations.OrderBy(r => r.ImplementationType))
            {
                sb.AppendLine($"        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof({ServiceType}), typeof({ImplementationType})));");
            }

            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
        }

        if (emitProfiles)
        {
            sb.AppendLine();
            sb.AppendLine("    public static IServiceCollection AddKyrolusGeneratedValidationProfiles(this IServiceCollection services)");
            sb.AppendLine("    {");
            sb.AppendLine("        services.TryAddEnumerable(ServiceDescriptor.Singleton<KyrolusValidationProfile>(KyrolusValidationProfiles.Create));");
            sb.AppendLine("        services.TryAddEnumerable(ServiceDescriptor.Singleton<KyrolusValidationProfile>(KyrolusValidationProfiles.Update));");
            sb.AppendLine("        services.TryAddEnumerable(ServiceDescriptor.Singleton<KyrolusValidationProfile>(KyrolusValidationProfiles.UiHints));");
            sb.AppendLine("        services.TryAddEnumerable(ServiceDescriptor.Singleton<KyrolusValidationProfile>(KyrolusValidationProfiles.BackgroundJobs));");
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");

        if (emitHookOrder)
        {
            sb.AppendLine();
            sb.AppendLine("public sealed class KyrolusGeneratedValidationHookOrderLookup : IKyrolusValidationHookOrderLookup");
            sb.AppendLine("{");
            sb.AppendLine("    public int? TryGetOrder(global::System.Type hookType)");
            sb.AppendLine("    {");
            foreach (var entry in hookOrders)
            {
                sb.AppendLine($"        if (hookType == typeof({entry.Key})) return {entry.Value};");
            }
            sb.AppendLine("        return null;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("public static class KyrolusValidationGeneratedHookOrderServiceCollectionExtensions");
            sb.AppendLine("{");
            sb.AppendLine("    public static IServiceCollection AddKyrolusGeneratedValidationHookOrder(this IServiceCollection services)");
            sb.AppendLine("    {");
            sb.AppendLine("        services.TryAddSingleton<IKyrolusValidationHookOrderLookup, KyrolusGeneratedValidationHookOrderLookup>();");
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        context.AddSource("KyrolusValidationGeneratedServiceCollectionExtensions.g.cs", sb.ToString());
    }
}
