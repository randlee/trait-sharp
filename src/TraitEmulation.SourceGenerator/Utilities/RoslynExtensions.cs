using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TraitEmulation.SourceGenerator.Utilities
{
    internal static class RoslynExtensions
    {
        public static string GetFullMetadataName(this INamedTypeSymbol symbol)
        {
            if (symbol.ContainingNamespace.IsGlobalNamespace)
                return symbol.Name;
            return $"{symbol.ContainingNamespace.ToDisplayString()}.{symbol.Name}";
        }

        public static string GetMinimalTypeName(this ITypeSymbol symbol)
        {
            return symbol.SpecialType switch
            {
                SpecialType.System_Byte => "byte",
                SpecialType.System_SByte => "sbyte",
                SpecialType.System_Boolean => "bool",
                SpecialType.System_Int16 => "short",
                SpecialType.System_UInt16 => "ushort",
                SpecialType.System_Char => "char",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_Single => "float",
                SpecialType.System_Int64 => "long",
                SpecialType.System_UInt64 => "ulong",
                SpecialType.System_Double => "double",
                SpecialType.System_String => "string",
                _ => symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            };
        }

        public static Dictionary<string, string> ParseFieldMapping(string? fieldMapping)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(fieldMapping))
                return result;

            foreach (var pair in fieldMapping!.Split(','))
            {
                var parts = pair.Trim().Split(':');
                if (parts.Length == 2)
                {
                    result[parts[0].Trim()] = parts[1].Trim();
                }
            }
            return result;
        }
    }
}
