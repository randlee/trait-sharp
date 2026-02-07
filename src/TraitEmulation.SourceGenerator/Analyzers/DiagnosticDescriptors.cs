using Microsoft.CodeAnalysis;

namespace TraitEmulation.SourceGenerator.Analyzers
{
    internal static class DiagnosticDescriptors
    {
        private const string Category = "TraitEmulation";

        public static readonly DiagnosticDescriptor TE0001_MissingRequiredField = new(
            id: "TE0001",
            title: "Missing required field",
            messageFormat: "Trait implementation missing required property '{0}' for trait '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0002_PropertyTypeMismatch = new(
            id: "TE0002",
            title: "Property type mismatch",
            messageFormat: "Property type mismatch for '{0}': expected '{1}', found '{2}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0003_FieldOrderMismatch = new(
            id: "TE0003",
            title: "Property offset mismatch",
            messageFormat: "Field offset mismatch for '{0}': expected offset {1}, found {2}. Trait fields must be contiguous in the implementing type.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0004_MissingStructLayout = new(
            id: "TE0004",
            title: "Missing [StructLayout] attribute",
            messageFormat: "Type '{0}' is missing [StructLayout] attribute. Required for trait layout verification.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0005_ExternalTypeNotFound = new(
            id: "TE0005",
            title: "External type not found",
            messageFormat: "External type '{0}' was not found",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0006_InvalidTraitMember = new(
            id: "TE0006",
            title: "Invalid trait member",
            messageFormat: "Trait interface '{0}' must contain only properties or methods",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0007_PropertyMustHaveGetter = new(
            id: "TE0007",
            title: "Trait property must have getter",
            messageFormat: "Trait property '{0}' in '{1}' must have a getter",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0008_CircularDependency = new(
            id: "TE0008",
            title: "Circular trait dependency",
            messageFormat: "Circular trait dependency detected involving '{0}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TE0009_NonContiguousFields = new(
            id: "TE0009",
            title: "Trait fields not contiguous",
            messageFormat: "Trait fields for '{0}' are not contiguous in type '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
