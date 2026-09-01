using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KyrolusSous.Validation.DataAnnotations.Generator;

/// <summary>
/// Roslyn incremental source generator that emits an <c>IKyrolusRequestValidator&lt;T&gt;</c> implementation at
/// compile time for every class decorated with a recognized <c>System.ComponentModel.DataAnnotations</c>
/// attribute - the AOT/trimming-friendly alternative to the reflection-based
/// <c>KyrolusSous.Validation.DataAnnotations</c> package's <c>DataAnnotationsRequestValidator&lt;T&gt;</c>. Attach
/// this package as an <c>Analyzer</c> reference and no other setup is required: matching classes are discovered
/// automatically, and each gets a generated <c>{ClassName}GeneratedDataAnnotationsValidator</c> in the
/// <c>KyrolusSous.Validation.Generated</c> namespace, plus a
/// <c>KyrolusDataAnnotationsGeneratedServiceCollectionExtensions.AddKyrolusGeneratedDataAnnotationsValidators()</c>
/// extension that registers all of them in one call.
/// </summary>
/// <remarks>
/// Only the attributes in <see cref="KnownAttributeNames"/> are translated into real checks; anything else
/// (a custom <c>System.ComponentModel.DataAnnotations.ValidationAttribute</c> subclass, or a BCL one not yet
/// supported) is reported via the <c>KYVALGEN001</c> compiler warning
/// (<see cref="UnsupportedAttributeDiagnostic"/>) rather than being silently skipped. A property tagged with
/// <c>[KyrolusValidationScope]</c> gets its RuleSets/Groups baked into the generated check, resolved against the
/// caller's <c>KyrolusValidationContext</c> at runtime the same way the reflection-based validator does.
/// </remarks>
/// <example>
/// <code>
/// public class CreateUserRequest
/// {
///     [Required, EmailAddress]
///     public string Email { get; set; } = string.Empty;
/// }
///
/// // Program.cs - no manual registration of CreateUserRequest needed:
/// builder.Services.AddKyrolusGeneratedDataAnnotationsValidators();
/// </code>
/// </example>
[Generator]
public sealed class KyrolusDataAnnotationsValidationGenerator : IIncrementalGenerator
{
    /// <summary>
    /// DataAnnotations attribute names this generator knows how to translate into a check. An attribute name not
    /// in this set (a custom <c>ValidationAttribute</c> subclass, or a BCL one this generator hasn't learned yet,
    /// e.g. <c>CompareAttribute</c>) is reported via <see cref="UnsupportedAttributeDiagnostic"/> instead of being
    /// silently dropped from the generated validator.
    /// </summary>
    private static readonly HashSet<string> KnownAttributeNames =
    [
        "RequiredAttribute", "StringLengthAttribute", "RangeAttribute", "MinLengthAttribute", "MaxLengthAttribute",
        "EmailAddressAttribute", "RegularExpressionAttribute", "PhoneAttribute", "CreditCardAttribute", "UrlAttribute"
    ];

    /// <summary>
    /// Name of the marker attribute (KyrolusSous.Validation.Abstractions.KyrolusValidationScopeAttribute) that tags
    /// a property with RuleSet/Group membership. It produces no check of its own, so it's excluded from both
    /// <see cref="KnownAttributeNames"/> checks and the unsupported-attribute diagnostic.
    /// </summary>
    private const string ScopeAttributeName = "KyrolusValidationScopeAttribute";

    private static readonly DiagnosticDescriptor UnsupportedAttributeDiagnostic = new(
        id: "KYVALGEN001",
        title: "DataAnnotations attribute not supported by the source generator",
        messageFormat: "Property '{0}.{1}' has attribute '{2}' which the Kyrolus DataAnnotations source generator does not translate into a check; it is skipped in the generated validator. Use the reflection-based DataAnnotationsRequestValidator instead for this property, or implement IKyrolusRequestValidator<T> manually.",
        category: "KyrolusSous.Validation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
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

                            if (KnownAttributeNames.Contains(attrClass.Name) || IsValidationAttribute(attrClass))
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

    /// <summary>True when <paramref name="symbol"/>'s inheritance chain reaches <c>System.ComponentModel.DataAnnotations.ValidationAttribute</c> - catching custom validation attribute subclasses that aren't in <see cref="KnownAttributeNames"/> by name.</summary>
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

    /// <summary>
    /// Generates one validator class per candidate (via <see cref="GenerateValidatorClass"/>), then - if any were
    /// actually emitted - one shared DI registration extension (via <see cref="GenerateDiExtensions"/>) covering
    /// all of them.
    /// </summary>
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
            var source = GenerateValidatorClass(classSymbol, context, out var validatorClassName, out var fullValidatorName);
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

    /// <summary>
    /// Emits the full source text of one generated <c>IKyrolusRequestValidator&lt;T&gt;</c>/<c>IKyrolusRequestValidatorWithContext&lt;T&gt;</c>
    /// implementation for <paramref name="classSymbol"/>: one check per recognized attribute on each of its
    /// annotated properties, plus the shared <c>AddFailure</c>/<c>IsValidCreditCardNumber</c> helpers. Returns
    /// <see langword="null"/> (and reports nothing) when <paramref name="classSymbol"/> has no annotated
    /// properties at all - it was only a syntactic candidate, not an actual match.
    /// </summary>
    /// <param name="classSymbol">The candidate class to generate a validator for.</param>
    /// <param name="context">Used to report the <c>KYVALGEN001</c> diagnostic for any unsupported attribute found.</param>
    /// <param name="validatorClassName">Receives the generated class's simple name (<c>{ClassName}GeneratedDataAnnotationsValidator</c>).</param>
    /// <param name="fullValidatorName">Receives the generated class's fully-qualified name, for use in the DI registration source.</param>
    /// <returns>The generated C# source text, or <see langword="null"/> if nothing was generated.</returns>
    private static string? GenerateValidatorClass(
        INamedTypeSymbol classSymbol,
        SourceProductionContext context,
        out string validatorClassName,
        out string fullValidatorName)
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
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Text.RegularExpressions;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using KyrolusSous.Validation.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace KyrolusSous.Validation.Generated;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {validatorClassName} : IKyrolusRequestValidator<{targetTypeFull}>, IKyrolusRequestValidatorWithContext<{targetTypeFull}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync({targetTypeFull} request, CancellationToken cancellationToken = default)");
        sb.AppendLine("        => ValidateCore(request, null, cancellationToken);");
        sb.AppendLine();
        sb.AppendLine($"    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync({targetTypeFull} request, KyrolusValidationContext context, CancellationToken cancellationToken = default)");
        sb.AppendLine("        => ValidateCore(request, context, cancellationToken);");
        sb.AppendLine();
        sb.AppendLine($"    private static ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCore({targetTypeFull} request, KyrolusValidationContext? context, CancellationToken cancellationToken)");
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

            var scopeAttr = prop.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == ScopeAttributeName);
            var ruleSetsLiteral = BuildStringArrayLiteral(GetScopeStringArray(scopeAttr, "RuleSets"));
            var groupsLiteral = BuildStringArrayLiteral(GetScopeStringArray(scopeAttr, "Groups"));

            foreach (var attr in prop.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null) continue;

                var attrName = attrClass.Name;
                if (attrName == ScopeAttributeName) continue;
                var customMsg = GetCustomErrorMessage(attr);

                if (attrName == "RequiredAttribute")
                {
                    var msg = ToLiteral(customMsg ?? $"The {propName} field is required.");
                    if (isString)
                    {
                        sb.AppendLine($"        if (string.IsNullOrWhiteSpace(request.{propName}))");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                    else if (propType.IsReferenceType || propType.NullableAnnotation == NullableAnnotation.Annotated)
                    {
                        sb.AppendLine($"        if (request.{propName} is null)");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
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

                    var msg = ToLiteral(customMsg ?? (minLen > 0
                        ? $"The field {propName} must be a string with a minimum length of {minLen} and maximum length of {maxLen}."
                        : $"The field {propName} must be a string with a maximum length of {maxLen}."));

                    if (minLen > 0)
                    {
                        sb.AppendLine($"        if (request.{propName} != null && (request.{propName}.Length < {minLen} || request.{propName}.Length > {maxLen}))");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                    else
                    {
                        sb.AppendLine($"        if (request.{propName} != null && request.{propName}.Length > {maxLen})");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                }
                else if (attrName == "RangeAttribute")
                {
                    if (attr.ConstructorArguments.Length >= 2)
                    {
                        var minVal = attr.ConstructorArguments[0].Value;
                        var maxVal = attr.ConstructorArguments[1].Value;
                        var msg = ToLiteral(customMsg ?? $"The field {propName} must be between {minVal} and {maxVal}.");

                        sb.AppendLine($"        if (request.{propName} < {minVal} || request.{propName} > {maxVal})");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                }
                else if (attrName == "MinLengthAttribute" && isString)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int minLen)
                    {
                        var msg = ToLiteral(customMsg ?? $"The field {propName} must be a string with a minimum length of {minLen}.");
                        sb.AppendLine($"        if (request.{propName} != null && request.{propName}.Length < {minLen})");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                }
                else if (attrName == "MaxLengthAttribute" && isString)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int maxLen)
                    {
                        var msg = ToLiteral(customMsg ?? $"The field {propName} must be a string with a maximum length of {maxLen}.");
                        sb.AppendLine($"        if (request.{propName} != null && request.{propName}.Length > {maxLen})");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                }
                else if (attrName == "EmailAddressAttribute" && isString)
                {
                    var msg = ToLiteral(customMsg ?? $"The {propName} field is not a valid e-mail address.");
                    sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}) && !Regex.IsMatch(request.{propName}, @\"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$\", RegexOptions.None, TimeSpan.FromMilliseconds(200)))");
                    sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                }
                else if (attrName == "RegularExpressionAttribute" && isString)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string pattern)
                    {
                        var msg = ToLiteral(customMsg ?? $"The field {propName} must match the regular expression '{pattern}'.");
                        var patternLiteral = ToLiteral(pattern);
                        sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}) && !Regex.IsMatch(request.{propName}, {patternLiteral}, RegexOptions.None, TimeSpan.FromMilliseconds(200)))");
                        sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    }
                }
                else if (attrName == "UrlAttribute" && isString)
                {
                    // Mirrors System.ComponentModel.DataAnnotations.UrlAttribute: a plain scheme-prefix check,
                    // not full URI validation.
                    var msg = ToLiteral(customMsg ?? $"The {propName} field is not a valid fully-qualified http, https, or ftp URL.");
                    sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}) &&");
                    sb.AppendLine($"            !(request.{propName}.StartsWith(\"http://\", StringComparison.OrdinalIgnoreCase) ||");
                    sb.AppendLine($"              request.{propName}.StartsWith(\"https://\", StringComparison.OrdinalIgnoreCase) ||");
                    sb.AppendLine($"              request.{propName}.StartsWith(\"ftp://\", StringComparison.OrdinalIgnoreCase)))");
                    sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                }
                else if (attrName == "PhoneAttribute" && isString)
                {
                    var msg = ToLiteral(customMsg ?? $"The {propName} field is not a valid phone number.");
                    sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}))");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var digitCount_{propName} = request.{propName}.Count(char.IsDigit);");
                    sb.AppendLine($"            var hasInvalidChar_{propName} = request.{propName}.Any(c => !char.IsDigit(c) && c is not (' ' or '-' or '.' or '(' or ')' or '+' or 'x'));");
                    sb.AppendLine($"            if (digitCount_{propName} < 7 || digitCount_{propName} > 15 || hasInvalidChar_{propName})");
                    sb.AppendLine($"                AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                    sb.AppendLine("        }");
                }
                else if (attrName == "CreditCardAttribute" && isString)
                {
                    var msg = ToLiteral(customMsg ?? $"The {propName} field is not a valid credit card number.");
                    sb.AppendLine($"        if (!string.IsNullOrEmpty(request.{propName}) && !{validatorClassName}.IsValidCreditCardNumber(request.{propName}))");
                    sb.AppendLine($"            AddFailure(failures, \"{propName}\", {msg}, {ruleSetsLiteral}, {groupsLiteral}, context);");
                }
                else if (!KnownAttributeNames.Contains(attrName))
                {
                    var location = attr.ApplicationSyntaxReference is { } syntaxRef
                        ? syntaxRef.GetSyntax().GetLocation()
                        : Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(UnsupportedAttributeDiagnostic, location, classSymbol.Name, propName, attrName));
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("        if (failures.Count == 0)");
        sb.AppendLine("            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(Array.Empty<KyrolusValidationFailure>());");
        sb.AppendLine();
        sb.AppendLine("        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures.ToArray());");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AddFailure(List<KyrolusValidationFailure> failures, string propertyName, string message, string[] ruleSets, string[] groups, KyrolusValidationContext? context)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Gate applies uniformly whether or not the property is tagged: an untagged property behaves like a");
        sb.AppendLine("        // Fluent rule with no RuleSets/Groups attached, which only runs for the default scope. A scope that");
        sb.AppendLine("        // doesn't match the requested context is dropped entirely rather than kept and mislabeled with");
        sb.AppendLine("        // whatever RuleSet the caller happened to request.");
        sb.AppendLine("        if (!KyrolusValidationScopeResolver.ShouldExecute(context?.RuleSets, ruleSets, context?.Groups, groups))");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (ruleSets.Length == 0 && groups.Length == 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            failures.Add(new KyrolusValidationFailure(propertyName, message));");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var ruleSet = ruleSets.Length > 0 ? KyrolusValidationScopeResolver.ResolveActiveRuleSet(ruleSets, context?.RuleSets) : null;");
        sb.AppendLine("        var groupsList = groups.Length > 0 ? (IReadOnlyList<string>)groups : null;");
        sb.AppendLine("        failures.Add(new KyrolusValidationFailure(propertyName, message, RuleSet: ruleSet, Groups: groupsList));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static bool IsValidCreditCardNumber(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        var sanitized = value.Replace(\"-\", \"\").Replace(\" \", \"\");");
        sb.AppendLine("        if (sanitized.Length < 13 || sanitized.Length > 19 || !sanitized.All(char.IsDigit)) return false;");
        sb.AppendLine();
        sb.AppendLine("        var sum = 0;");
        sb.AppendLine("        var isSecond = false;");
        sb.AppendLine("        for (var i = sanitized.Length - 1; i >= 0; i--)");
        sb.AppendLine("        {");
        sb.AppendLine("            var d = sanitized[i] - '0';");
        sb.AppendLine("            if (isSecond)");
        sb.AppendLine("            {");
        sb.AppendLine("                d *= 2;");
        sb.AppendLine("                if (d > 9) d -= 9;");
        sb.AppendLine("            }");
        sb.AppendLine("            sum += d;");
        sb.AppendLine("            isSecond = !isSecond;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return sum % 10 == 0;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Renders a C# string literal for embedding arbitrary user-supplied text (a regex pattern, a custom
    /// ErrorMessage) into generated source. <see cref="string.Replace(string, string)"/>-based manual escaping
    /// only handled quotes, not backslashes - so a pattern like <c>\d{3}</c> produced generated code with an
    /// unrecognized escape sequence (CS1009) and failed to compile. SymbolDisplay.FormatLiteral is Roslyn's own
    /// helper for this and handles every case correctly, including the surrounding quotes.
    /// </summary>
    private static string ToLiteral(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>
    /// Reads the string-array value of a named argument (<c>RuleSets</c> or <c>Groups</c>) off a
    /// <c>[KyrolusValidationScope(...)]</c> attribute application, if present.
    /// </summary>
    private static string[] GetScopeStringArray(AttributeData? scopeAttr, string argumentName)
    {
        if (scopeAttr is null) return [];

        foreach (var namedArg in scopeAttr.NamedArguments)
        {
            if (namedArg.Key == argumentName && namedArg.Value.Kind == TypedConstantKind.Array)
            {
                return namedArg.Value.Values
                    .Select(v => v.Value as string)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToArray();
            }
        }

        return [];
    }

    /// <summary>Renders a <c>string[]</c> value (RuleSets/Groups tags) as a C# array-initializer expression.</summary>
    private static string BuildStringArrayLiteral(string[] values) =>
        values.Length == 0 ? "Array.Empty<string>()" : $"new[] {{ {string.Join(", ", values.Select(ToLiteral))} }}";

    /// <summary>Reads the <c>ErrorMessage</c> named argument off a DataAnnotations attribute application, if the developer supplied one; returns <see langword="null"/> to fall back to the generator's own default message.</summary>
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

    /// <summary>
    /// Emits <c>KyrolusDataAnnotationsGeneratedServiceCollectionExtensions.AddKyrolusGeneratedDataAnnotationsValidators()</c>,
    /// registering every generated validator (sorted by class name for deterministic output) via <c>TryAddEnumerable</c>.
    /// </summary>
    /// <param name="validators">Every validator class successfully generated by <see cref="GenerateValidatorClass"/> in this compilation.</param>
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
