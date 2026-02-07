using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace TraitEmulation.SourceGenerator.Models
{
    /// <summary>
    /// Local mirror of TraitEmulation.ImplStrategy to avoid runtime dependency on Attributes assembly.
    /// </summary>
    internal enum ImplStrategy
    {
        Auto = 0,
        Reinterpret = 1,
        FieldMapping = 2
    }

    internal sealed class ImplementationModel
    {
        public string TypeName { get; set; } = "";
        public string FullTypeName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string TypeKind { get; set; } = "struct";
        public string TraitInterfaceName { get; set; } = "";
        public string TraitFullName { get; set; } = "";
        public TraitModel? Trait { get; set; }
        public int BaseOffset { get; set; }
        public bool IsLayoutCompatible { get; set; }
        public Dictionary<string, string> FieldMapping { get; set; } = new Dictionary<string, string>();
        public INamedTypeSymbol? TypeSymbol { get; set; }
        public Location? Location { get; set; }
        public ImplStrategy Strategy { get; set; } = ImplStrategy.Auto;
    }

    internal sealed class ExternalImplModel
    {
        public string TraitInterfaceName { get; set; } = "";
        public string TraitFullName { get; set; } = "";
        public string TargetTypeName { get; set; } = "";
        public string TargetFullTypeName { get; set; } = "";
        public string TargetNamespace { get; set; } = "";
        public TraitModel? Trait { get; set; }
        public int BaseOffset { get; set; }
        public bool IsLayoutCompatible { get; set; }
        public Dictionary<string, string> FieldMapping { get; set; } = new Dictionary<string, string>();
        public INamedTypeSymbol? TargetTypeSymbol { get; set; }
        public Location? Location { get; set; }
        public ImplStrategy Strategy { get; set; } = ImplStrategy.Auto;
    }
}
