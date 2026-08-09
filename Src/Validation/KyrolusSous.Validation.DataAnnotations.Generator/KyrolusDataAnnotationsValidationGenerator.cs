using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KyrolusSous.Validation.DataAnnotations.Generator;

[Generator]
public sealed class KyrolusDataAnnotationsValidationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateClasses = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax classDecl && classDecl.Members.OfType<PropertyDeclarationSyntax>().Any(),
            static (ctx, _) =>
            {
                var classDecl = (ClassDeclarationSyntax)ctx.Node;
                var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (symbol is null || symbol.IsAbstract || symbol.TypeParameters.Length > 0)
                {
                    return null;
                }

                bool hasDataAnnotations = false;
                foreach (var member in symbol.GetMembers())
                {
                    if (member is IPropertySymbol propertySymbol)
                    {
                        foreach (var attr in propertySymbol.GetAttributes())
                        {
                            var attrClass = attr.AttributeClass;
                            if (attrClass is null) continue;

                            var name = attrClass.Name;
                            if (name is "RequiredAttribute" or "StringLengthAttribute" or "RangeAttribute"
                                or "MinLengthAttribute" or "MaxLengthAttribute" or "EmailAddressAttribute"
                                or "RegularExpressionAttribute" or "PhoneAttribute" or "CreditCardAttribute" or "UrlAttribute"
                                || IsValidationAttribute(attrClass))
                            {
                                hasDataAnnotations = true;
                                break;
                            }
                        }
                    }
                    if (hasDataAnnotations) break;
                }

                return hasDataAnnotations ? symbol : null;
            })
            .Where(static symbol => symbol is not null);

        var compilationAndCandidates = context.CompilationProvider.Combine(candidateClasses.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, pair) =>
        {
            var candidates = pair.Right.OfType<INamedTypeSymbol>().ToImmutableArray();
            Emit(spc, pair.Left, candidates);
        });
    }

    private static bool IsValidationAttribute(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current is not null)
        {
            if (current.ToDisplayString() == "System.ComponentModel.DataAnnotations.ValidationAttribute")
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static void Emit(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> candidates)
    {
        if (candidates.IsEmpty)
        {
            return;
        }

        var distinctCandidates = candidates.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
        var generatedValidators = new List<(string TargetType, string ValidatorClassName, string FullValidatorName)>();

        foreach (var classSymbol in distinctCandidates)
        {
            var source = GenerateValidatorClass(classSymbol, out var validatorClassName, out var fullValidatorName);
            if (source is not null)
            {
                context.AddSource($"{validatorClassName}.g.cs", source);
                generatedValidators.Add((classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), validatorClassName, fullValidatorName));
            }
        }

        if (generatedValidators.Count > 0)
        {
            var diExtensionSource = GenerateDiExtensions(generatedValidators);
            context.AddSource("KyrolusDataAnnotationsGeneratedServiceCollectionExtensions.g.cs", diExtensionSource);
        }
    }

    private static string? GenerateValidatorClass(INamedTypeSymbol classSymbol, out string validatorClassName, out string fullValidatorName)
    {
        var className = classSymbol.Name;
        validatorClassName = $"{className}GeneratedDataAnnotationsValidator";
        fullValidatorName = $"KyrolusSous.Validation.Generated.{validatorClassName}";
        var targetTypeFull = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var properties = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes().Length > 0)
            .ToList();

        if (properties.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.RegularExpressions;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using KyrolusSous.Validation.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace KyrolusSous.Validation.Generated;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {validatorClassName} : IKyrolusRequestValidator<{targetTypeFull}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync({targetTypeFull} request, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (request is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([new KyrolusValidationFailure(string.Empty, \"Request is required.\")]);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var failures = new List<KyrolusValidationFailure>();");

        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var propType = prop.Type;
            var isString = propType.SpecialType == SpecialType.System_String;

            foreach (var attr in prop.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null) continue;

                var attrName = attrClass.Name;
                var customMsg = GetCustomErrorMessage(attr);

                if (attrName == "RequiredAttribute")
                {
                    var msg = customMsg ?? $"The {propName} field is required.";
                    if (isString)
                    {
                        sb.AppendLine($"        if (string.IsNullOrWhiteSpace(request.{propName}))");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                    else if (propType.IsReferenceType || propType.NullableAnnotation == NullableAnnotation.Annotated)
                    {
                        sb.AppendLine($"        if (request.{propName} is null)");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                }
                else if (attrName == "StringLengthAttribute" && isString)
                {
                    int maxLen = 0;
                    int minLen = 0;

                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int max)
                    {
                        maxLen = max;
                    }

                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "MinimumLength" && namedArg.Value.Value is int min)
                        {
                            minLen = min;
                        }
                    }

                    var msg = customMsg ?? (minLen > 0
                        ? $"The field {propName} must be a string with a minimum length of {minLen} and maximum length of {maxLen}."
                        : $"The field {propName} must be a string with a maximum length of {maxLen}.");

                    if (minLen > 0)
                    {
                        sb.AppendLine($"        if (request.{propName} != null && (request.{propName}.Length < {minLen} || request.{propName}.Length > {maxLen}))");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                    else
                    {
                        sb.AppendLine($"        if (request.{propName} != null && request.{propName}.Length > {maxLen})");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                }
                else if (attrName == "RangeAttribute")
                {
                    if (attr.ConstructorArguments.Length >= 2)
                    {
                        var minVal = attr.ConstructorArguments[0].Value;
                        var maxVal = attr.ConstructorArguments[1].Value;
                        var msg = customMsg ?? $"The field {propName} must be between {minVal} and {maxVal}.";

                        sb.AppendLine($"        if (request.{propName} < {minVal} || request.{propName} > {maxVal})");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                }
                else if (attrName == "MinLengthAttribute" && isString)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int minLen)
                    {
                        var msg = customMsg ?? $"The field {propName} must be a string with a minimum length of {minLen}.";
                        sb.AppendLine($"        if (request.{propName} != null && request.{propName}.Length < {minLen})");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                }
                else if (attrName == "MaxLengthAttribute" && isString)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int maxLen)
                    {
                        var msg = customMsg ?? $"The field {propName} must be a string with a maximum length of {maxLen}.";
                        sb.AppendLine($"        if (request.{propName} != null && request.{propName}.Length > {maxLen})");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                }
                else if (attrName == "EmailAddressAttribute" && isString)
                {
                    var msg = customMsg ?? $"The {propName} field is not a valid e-mail address.";
                    sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}) && (!request.{propName}.Contains(\"@\") || !request.{propName}.Contains(\".\")))");
                    sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                }
                else if (attrName == "RegularExpressionAttribute" && isString)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string pattern)
                    {
                        var msg = customMsg ?? $"The field {propName} must match the regular expression '{pattern}'.";
                        var escapedPattern = pattern.Replace("\"", "\\\"");
                        sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}) && !Regex.IsMatch(request.{propName}, \"{escapedPattern}\"))");
                        sb.AppendLine($"            failures.Add(new KyrolusValidationFailure(\"{propName}\", \"{msg}\"));");
                    }
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("        if (failures.Count == 0)");
        sb.AppendLine("            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(Array.Empty<KyrolusValidationFailure>());");
        sb.AppendLine();
        sb.AppendLine("        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures.ToArray());");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string? GetCustomErrorMessage(AttributeData attr)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "ErrorMessage" && arg.Value.Value is string msg)
            {
                return msg;
            }
        }
        return null;
    }

    private static string GenerateDiExtensions(List<(string TargetType, string ValidatorClassName, string FullValidatorName)> validators)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using KyrolusSous.Validation.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace KyrolusSous.Validation.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class KyrolusDataAnnotationsGeneratedServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection AddKyrolusGeneratedDataAnnotationsValidators(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var v in validators.OrderBy(x => x.ValidatorClassName))
        {
            sb.AppendLine($"        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusRequestValidator<{v.TargetType}>), typeof({v.FullValidatorName})));");
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
