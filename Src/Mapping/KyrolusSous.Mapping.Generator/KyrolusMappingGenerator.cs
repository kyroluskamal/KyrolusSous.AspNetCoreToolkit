using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.CompilerServices;
using KyrolusSous.Mapping.Generator.Models;
using KyrolusSous.Mapping.Generator.Diagnostics;

[assembly: InternalsVisibleTo("KyrolusSous.Mapping.UnitTests")]

namespace KyrolusSous.Mapping.Generator;

/// <summary>
/// Incremental source generator that emits direct, compile-time object-to-object mapping extension methods and partial mapper classes.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class KyrolusMappingGenerator : IIncrementalGenerator
{
    private const string MapToAttributeName = "KyrolusSous.Mapping.Abstractions.Attributes.KyrolusMapToAttribute";
    private const string MapFromAttributeName = "KyrolusSous.Mapping.Abstractions.Attributes.KyrolusMapFromAttribute";
    private const string MapperAttributeName = "KyrolusSous.Mapping.Abstractions.Attributes.KyrolusMapperAttribute";
    private const string IgnoreMapAttributeName = "KyrolusSous.Mapping.Abstractions.Attributes.KyrolusIgnoreMapAttribute";
    private const string MapPropertyAttributeName = "KyrolusSous.Mapping.Abstractions.Attributes.KyrolusMapPropertyAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Discover types decorated with [KyrolusMapTo] or [KyrolusMapFrom]
        var candidateExtensionTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, ct) => GetTypeMappingPairs(ctx, ct))
            .Where(static pairs => pairs.Count > 0)
            .SelectMany(static (pairs, _) => pairs);

        var collectedPairs = candidateExtensionTypes.Collect();

        context.RegisterSourceOutput(collectedPairs, static (spc, pairs) =>
        {
            if (pairs.IsEmpty)
            {
                return;
            }

            var distinctPairs = pairs.Distinct().ToList();
            var sourceCode = EmitMappingExtensions(distinctPairs);
            spc.AddSource("KyrolusGeneratedMappingExtensions.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        });

        // 2. Discover partial mapper classes decorated with [KyrolusMapper]
        var candidateMapperClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, ct) => GetMapperClassModel(ctx, ct))
            .Where(static cls => cls is not null);

        var collectedMapperClasses = candidateMapperClasses.Collect();

        context.RegisterSourceOutput(collectedMapperClasses, static (spc, classes) =>
        {
            if (classes.IsEmpty)
            {
                return;
            }

            foreach (var mapperClass in classes)
            {
                if (mapperClass is null || mapperClass.Methods.Count == 0)
                {
                    continue;
                }

                var code = EmitPartialMapperClass(mapperClass);
                spc.AddSource($"{mapperClass.ClassName}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }

    private static KyrolusMapperClassModel? GetMapperClassModel(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;

        if (symbol is null)
        {
            return null;
        }

        var hasMapperAttr = symbol.GetAttributes().Any(a =>
        {
            var fullName = a.AttributeClass?.ToDisplayString();
            var shortName = a.AttributeClass?.Name;
            return fullName == MapperAttributeName || shortName == "KyrolusMapperAttribute" || shortName == "KyrolusMapper" || shortName == "MapperAttribute" || shortName == "Mapper";
        });

        if (!hasMapperAttr)
        {
            return null;
        }

        var model = new KyrolusMapperClassModel
        {
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString(),
            ClassName = symbol.Name,
            IsStatic = symbol.IsStatic
        };

        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.IsPartialDefinition)
            {
                if (member.Parameters.Length == 1 && member.ReturnType is INamedTypeSymbol returnType && returnType.SpecialType != SpecialType.System_Void)
                {
                    var sourceParam = member.Parameters[0];
                    if (sourceParam.Type is INamedTypeSymbol sourceType)
                    {
                        var pair = BuildTypePairModel(sourceType, returnType);
                        model.Methods.Add(new KyrolusMapperMethodModel
                        {
                            MethodName = member.Name,
                            SourceTypeName = sourceType.Name,
                            TargetTypeName = returnType.Name,
                            SourceFullTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            TargetFullTypeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            IsStatic = member.IsStatic,
                            IsInPlace = false,
                            TypePair = pair
                        });
                    }
                }
                else if (member.Parameters.Length == 2 && member.ReturnsVoid)
                {
                    var sourceParam = member.Parameters[0];
                    var targetParam = member.Parameters[1];
                    if (sourceParam.Type is INamedTypeSymbol sourceType && targetParam.Type is INamedTypeSymbol targetType)
                    {
                        var pair = BuildTypePairModel(sourceType, targetType);
                        model.Methods.Add(new KyrolusMapperMethodModel
                        {
                            MethodName = member.Name,
                            SourceTypeName = sourceType.Name,
                            TargetTypeName = targetType.Name,
                            SourceFullTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            TargetFullTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            IsStatic = member.IsStatic,
                            IsInPlace = true,
                            TypePair = pair
                        });
                    }
                }
            }
        }

        return model;
    }

    private static List<KyrolusTypePairMappingModel> GetTypeMappingPairs(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var result = new List<KyrolusTypePairMappingModel>();
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct) as INamedTypeSymbol;

        if (symbol is null)
        {
            return result;
        }

        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass?.ToDisplayString();
            var attrShortName = attr.AttributeClass?.Name;
            var isBidirectional = false;

            if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is bool bPos)
            {
                isBidirectional = bPos;
            }

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "IsBidirectional" && named.Value.Value is bool bNamed)
                {
                    isBidirectional = bNamed;
                }
            }

            if ((attrClass == MapToAttributeName || attrShortName == "KyrolusMapToAttribute" || attrShortName == "KyrolusMapTo") && attr.ConstructorArguments.Length > 0)
            {
                if (attr.ConstructorArguments[0].Value is INamedTypeSymbol targetSymbol)
                {
                    result.Add(BuildTypePairModel(symbol, targetSymbol));
                    if (isBidirectional)
                    {
                        result.Add(BuildTypePairModel(targetSymbol, symbol));
                    }
                }
            }
            else if ((attrClass == MapFromAttributeName || attrShortName == "KyrolusMapFromAttribute" || attrShortName == "KyrolusMapFrom") && attr.ConstructorArguments.Length > 0)
            {
                if (attr.ConstructorArguments[0].Value is INamedTypeSymbol sourceSymbol)
                {
                    result.Add(BuildTypePairModel(sourceSymbol, symbol));
                    if (isBidirectional)
                    {
                        result.Add(BuildTypePairModel(symbol, sourceSymbol));
                    }
                }
            }
        }

        return result;
    }

    private static KyrolusTypePairMappingModel BuildTypePairModel(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var model = new KyrolusTypePairMappingModel
        {
            SourceTypeName = source.Name,
            TargetTypeName = target.Name,
            SourceFullTypeName = source.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TargetFullTypeName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MethodName = $"To{target.Name}"
        };

        var sourceProps = GetAllProperties(source)
            .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && p.GetMethod is not null)
            .ToDictionary(p => p.Name, p => p, System.StringComparer.OrdinalIgnoreCase);

        // Check target primary constructor / positional record parameters
        var constructors = target.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        var primaryCtor = constructors.FirstOrDefault(c => c.Parameters.Length > 0);
        if (target.IsRecord && primaryCtor is not null)
        {
            model.IsTargetPositionalRecord = true;
            foreach (var param in primaryCtor.Parameters)
            {
                var paramName = param.Name;
                if (sourceProps.TryGetValue(paramName, out var matchedSourceProp))
                {
                    model.ConstructorParameters.Add(new KyrolusPropertyMappingModel
                    {
                        TargetPropertyName = paramName,
                        SourcePropertyName = matchedSourceProp.Name,
                        TargetPropertyType = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        SourcePropertyType = matchedSourceProp.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        IsConstructorParameter = true,
                        IsDirectAssignment = SymbolEqualityComparer.Default.Equals(param.Type, matchedSourceProp.Type)
                    });
                }
            }
        }

        // Map writable properties
        var targetProps = GetAllProperties(target)
            .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && p.SetMethod is not null);

        foreach (var targetProp in targetProps)
        {
            if (HasAttribute(targetProp, IgnoreMapAttributeName, "KyrolusIgnoreMapAttribute", "KyrolusIgnoreMap"))
            {
                continue;
            }

            var sourceLookupName = GetCustomSourcePropertyName(targetProp) ?? targetProp.Name;
            if (sourceProps.TryGetValue(sourceLookupName, out var sourceProp))
            {
                if (HasAttribute(sourceProp, IgnoreMapAttributeName, "KyrolusIgnoreMapAttribute", "KyrolusIgnoreMap"))
                {
                    continue;
                }

                model.Properties.Add(new KyrolusPropertyMappingModel
                {
                    TargetPropertyName = targetProp.Name,
                    SourcePropertyName = sourceProp.Name,
                    TargetPropertyType = targetProp.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    SourcePropertyType = sourceProp.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    IsDirectAssignment = SymbolEqualityComparer.Default.Equals(targetProp.Type, sourceProp.Type)
                });
            }
        }

        return model;
    }

    private static List<IPropertySymbol> GetAllProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var current = type;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            properties.AddRange(current.GetMembers().OfType<IPropertySymbol>());
            current = current.BaseType;
        }

        return properties;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName, string attributeShortName1, string attributeShortName2)
    {
        return symbol.GetAttributes().Any(a =>
        {
            var fullName = a.AttributeClass?.ToDisplayString();
            var shortName = a.AttributeClass?.Name;
            return fullName == attributeFullName || shortName == attributeShortName1 || shortName == attributeShortName2;
        });
    }

    private static string? GetCustomSourcePropertyName(IPropertySymbol property)
    {
        var attr = property.GetAttributes().FirstOrDefault(a =>
        {
            var fullName = a.AttributeClass?.ToDisplayString();
            var shortName = a.AttributeClass?.Name;
            return fullName == MapPropertyAttributeName || shortName == "KyrolusMapPropertyAttribute" || shortName == "KyrolusMapProperty";
        });

        if (attr is not null && attr.ConstructorArguments.Length > 0)
        {
            return attr.ConstructorArguments[0].Value as string;
        }

        return null;
    }

    private static string EmitMappingExtensions(List<KyrolusTypePairMappingModel> pairs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace KyrolusSous.Mapping.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Compile-time direct assignment mapping extension methods generated by KyrolusSous.Mapping.Generator.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class KyrolusGeneratedMappingExtensions");
        sb.AppendLine("    {");

        foreach (var pair in pairs)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Directly maps <see cref=\"{pair.SourceFullTypeName}\"/> to a new <see cref=\"{pair.TargetFullTypeName}\"/> without reflection.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public static {pair.TargetFullTypeName}? {pair.MethodName}(this {pair.SourceFullTypeName}? source)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (source is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return default;");
            sb.AppendLine("            }");
            sb.AppendLine();

            if (pair.IsTargetPositionalRecord && pair.ConstructorParameters.Count > 0)
            {
                var nonCtorProps = pair.Properties.Where(p => !pair.ConstructorParameters.Any(cp => cp.TargetPropertyName.Equals(p.TargetPropertyName, System.StringComparison.OrdinalIgnoreCase))).ToList();
                if (nonCtorProps.Count > 0)
                {
                    sb.AppendLine($"            var target = new {pair.TargetFullTypeName}(" + string.Join(", ", pair.ConstructorParameters.Select(p => $"source.{p.SourcePropertyName}")) + ");");
                    foreach (var prop in nonCtorProps)
                    {
                        sb.AppendLine($"            target.{prop.TargetPropertyName} = source.{prop.SourcePropertyName};");
                    }
                    sb.AppendLine("            return target;");
                }
                else
                {
                    sb.Append($"            return new {pair.TargetFullTypeName}(");
                    var args = pair.ConstructorParameters.Select(p => $"source.{p.SourcePropertyName}");
                    sb.Append(string.Join(", ", args));
                    sb.AppendLine(");");
                }
            }
            else
            {
                sb.AppendLine($"            var target = new {pair.TargetFullTypeName}();");
                foreach (var prop in pair.Properties)
                {
                    sb.AppendLine($"            target.{prop.TargetPropertyName} = source.{prop.SourcePropertyName};");
                }
                sb.AppendLine("            return target;");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EmitPartialMapperClass(KyrolusMapperClassModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var hasNamespace = !string.IsNullOrWhiteSpace(model.Namespace);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        var indent = hasNamespace ? "    " : "";
        var staticMod = model.IsStatic ? "static " : "";

        sb.AppendLine($"{indent}public {staticMod}partial class {model.ClassName}");
        sb.AppendLine($"{indent}{{");

        foreach (var method in model.Methods)
        {
            var pair = method.TypePair;
            var methodStaticMod = method.IsStatic ? "static " : "";

            if (method.IsInPlace)
            {
                sb.AppendLine($"{indent}    public {methodStaticMod}partial void {method.MethodName}({method.SourceFullTypeName} source, {method.TargetFullTypeName} target)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (source is null || target is null) return;");
                foreach (var prop in pair.Properties)
                {
                    sb.AppendLine($"{indent}        target.{prop.TargetPropertyName} = source.{prop.SourcePropertyName};");
                }
                sb.AppendLine($"{indent}    }}");
            }
            else
            {
                sb.AppendLine($"{indent}    public {methodStaticMod}partial {method.TargetFullTypeName} {method.MethodName}({method.SourceFullTypeName} source)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (source is null) return default!;");

                if (pair.IsTargetPositionalRecord && pair.ConstructorParameters.Count > 0)
                {
                    var nonCtorProps = pair.Properties.Where(p => !pair.ConstructorParameters.Any(cp => cp.TargetPropertyName.Equals(p.TargetPropertyName, System.StringComparison.OrdinalIgnoreCase))).ToList();
                    if (nonCtorProps.Count > 0)
                    {
                        sb.AppendLine($"{indent}        var target = new {pair.TargetFullTypeName}(" + string.Join(", ", pair.ConstructorParameters.Select(p => $"source.{p.SourcePropertyName}")) + ");");
                        foreach (var prop in nonCtorProps)
                        {
                            sb.AppendLine($"{indent}        target.{prop.TargetPropertyName} = source.{prop.SourcePropertyName};");
                        }
                        sb.AppendLine($"{indent}        return target;");
                    }
                    else
                    {
                        sb.Append($"{indent}        return new {pair.TargetFullTypeName}(");
                        var args = pair.ConstructorParameters.Select(p => $"source.{p.SourcePropertyName}");
                        sb.Append(string.Join(", ", args));
                        sb.AppendLine(");");
                    }
                }
                else
                {
                    sb.AppendLine($"{indent}        var target = new {pair.TargetFullTypeName}();");
                    foreach (var prop in pair.Properties)
                    {
                        sb.AppendLine($"{indent}        target.{prop.TargetPropertyName} = source.{prop.SourcePropertyName};");
                    }
                    sb.AppendLine($"{indent}        return target;");
                }

                sb.AppendLine($"{indent}    }}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"{indent}}}");

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}
