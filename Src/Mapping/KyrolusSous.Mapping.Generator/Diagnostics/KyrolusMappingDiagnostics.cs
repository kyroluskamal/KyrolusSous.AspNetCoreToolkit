using Microsoft.CodeAnalysis;

namespace KyrolusSous.Mapping.Generator.Diagnostics;

internal static class KyrolusMappingDiagnostics
{
    public static readonly DiagnosticDescriptor UnmappedProperty = new(
        id: "KYMAP001",
        title: "Target property has no matching source property",
        messageFormat: "Target property '{0}.{1}' does not have a matching property on source type '{2}' and was not ignored",
        category: "KyrolusSous.Mapping",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatiblePropertyType = new(
        id: "KYMAP002",
        title: "Incompatible mapping property types",
        messageFormat: "Cannot automatically convert property '{0}.{1}' of type '{2}' to target property '{3}.{4}' of type '{5}'",
        category: "KyrolusSous.Mapping",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
