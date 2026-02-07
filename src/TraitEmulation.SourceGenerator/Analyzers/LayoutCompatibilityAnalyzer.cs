using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using TraitEmulation.SourceGenerator.Models;

namespace TraitEmulation.SourceGenerator.Analyzers
{
    internal static class LayoutCompatibilityAnalyzer
    {
        public static LayoutAnalysis Analyze(
            INamedTypeSymbol implementationType,
            TraitModel trait,
            Dictionary<string, string> fieldMapping,
            Action<Diagnostic> reportDiagnostic)
        {
            var result = new LayoutAnalysis
            {
                IsCompatible = true,
                BaseOffset = 0
            };

            // Check StructLayout attribute — hard requirement
            var layoutAttr = implementationType.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "StructLayoutAttribute");

            if (layoutAttr == null)
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TE0004_MissingStructLayout,
                    implementationType.Locations.FirstOrDefault(),
                    implementationType.Name));
                result.IsCompatible = false;
                return result;
            }

            // Verify it's Sequential or Explicit
            if (layoutAttr.ConstructorArguments.Length > 0)
            {
                var layoutKindValue = (int)layoutAttr.ConstructorArguments[0].Value!;
                // LayoutKind.Sequential = 0, Explicit = 2, Auto = 3
                if (layoutKindValue != 0 && layoutKindValue != 2)
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TE0004_MissingStructLayout,
                        implementationType.Locations.FirstOrDefault(),
                        implementationType.Name));
                    result.IsCompatible = false;
                    return result;
                }
            }

            if (trait.Properties.Count == 0)
                return result;

            // Find the first trait property to determine base offset
            var firstTraitProp = trait.Properties[0];
            var firstName = GetMappedFieldName(firstTraitProp.Name, fieldMapping);
            var firstField = FindMatchingField(implementationType, firstName);

            if (firstField == null)
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TE0001_MissingRequiredField,
                    implementationType.Locations.FirstOrDefault(),
                    firstTraitProp.Name, trait.Name));
                result.IsCompatible = false;
                return result;
            }

            int baseOffset = CalculateFieldOffset(firstField, implementationType);
            result.BaseOffset = baseOffset;

            // Verify each trait property is contiguous from baseOffset
            int expectedOffset = baseOffset;
            foreach (var traitProp in trait.Properties)
            {
                var mappedName = GetMappedFieldName(traitProp.Name, fieldMapping);
                var field = FindMatchingField(implementationType, mappedName);

                if (field == null)
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TE0001_MissingRequiredField,
                        implementationType.Locations.FirstOrDefault(),
                        traitProp.Name, trait.Name));
                    result.IsCompatible = false;
                    continue;
                }

                // Type check
                if (!SymbolEquals(field.Type, traitProp.Type!))
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TE0002_PropertyTypeMismatch,
                        field.Locations.FirstOrDefault() ?? implementationType.Locations.FirstOrDefault(),
                        traitProp.Name, traitProp.TypeName, field.Type.ToDisplayString()));
                    result.IsCompatible = false;
                    continue;
                }

                // Offset check — must be contiguous from base
                var actualOffset = CalculateFieldOffset(field, implementationType);
                if (actualOffset != expectedOffset)
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TE0003_FieldOrderMismatch,
                        field.Locations.FirstOrDefault() ?? implementationType.Locations.FirstOrDefault(),
                        traitProp.Name, expectedOffset, actualOffset));
                    result.IsCompatible = false;
                }

                expectedOffset += GetTypeSize(field.Type);
            }

            return result;
        }

        private static string GetMappedFieldName(string traitPropertyName, Dictionary<string, string> fieldMapping)
        {
            if (fieldMapping != null && fieldMapping.TryGetValue(traitPropertyName, out var mapped))
                return mapped;
            return traitPropertyName;
        }

        private static IFieldSymbol? FindMatchingField(INamedTypeSymbol type, string name)
        {
            return type.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => !f.IsStatic && f.Name == name);
        }

        private static bool SymbolEquals(ITypeSymbol a, ITypeSymbol b)
        {
            return SymbolEqualityComparer.Default.Equals(a, b);
        }

        internal static int CalculateFieldOffset(IFieldSymbol field, INamedTypeSymbol type)
        {
            // Check for explicit [FieldOffset]
            var offsetAttr = field.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "FieldOffsetAttribute");
            if (offsetAttr != null && offsetAttr.ConstructorArguments.Length > 0)
            {
                return (int)offsetAttr.ConstructorArguments[0].Value!;
            }

            // For Sequential layout, sum sizes of preceding fields with alignment
            int offset = 0;
            foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.IsStatic) continue;
                if (SymbolEqualityComparer.Default.Equals(member, field))
                {
                    // Align the target field itself
                    int targetSize = GetTypeSize(field.Type);
                    int targetAlignment = Math.Min(targetSize, 8);
                    if (targetAlignment > 0)
                    {
                        offset = (offset + targetAlignment - 1) & ~(targetAlignment - 1);
                    }
                    break;
                }
                int fieldSize = GetTypeSize(member.Type);
                // Align to natural boundary: min(fieldSize, packingSize)
                int alignment = Math.Min(fieldSize, 8); // Default packing = 8
                if (alignment > 0)
                {
                    offset = (offset + alignment - 1) & ~(alignment - 1);
                }
                offset += fieldSize;
            }

            return offset;
        }

        internal static int GetTypeSize(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Byte => 1,
                SpecialType.System_SByte => 1,
                SpecialType.System_Boolean => 1,
                SpecialType.System_Int16 => 2,
                SpecialType.System_UInt16 => 2,
                SpecialType.System_Char => 2,
                SpecialType.System_Int32 => 4,
                SpecialType.System_UInt32 => 4,
                SpecialType.System_Single => 4,
                SpecialType.System_Int64 => 8,
                SpecialType.System_UInt64 => 8,
                SpecialType.System_Double => 8,
                SpecialType.System_IntPtr => 8, // Assume 64-bit
                SpecialType.System_UIntPtr => 8,
                _ => GetStructSize(type)
            };
        }

        private static int GetStructSize(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol namedType || !namedType.IsValueType)
                return 0;

            int size = 0;
            int maxAlignment = 1;
            foreach (var member in namedType.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.IsStatic) continue;
                int fieldSize = GetTypeSize(member.Type);
                int alignment = Math.Min(fieldSize, 8);
                maxAlignment = Math.Max(maxAlignment, alignment);
                if (alignment > 0)
                {
                    size = (size + alignment - 1) & ~(alignment - 1);
                }
                size += fieldSize;
            }
            // Pad to struct alignment
            if (maxAlignment > 0)
            {
                size = (size + maxAlignment - 1) & ~(maxAlignment - 1);
            }
            return size;
        }
    }
}
