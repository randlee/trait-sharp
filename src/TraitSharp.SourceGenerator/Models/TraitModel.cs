using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace TraitSharp.SourceGenerator.Models
{
    internal sealed class TraitModel
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string LayoutStructName { get; set; } = "";
        public bool GenerateLayout { get; set; } = true;
        public bool GenerateExtensions { get; set; } = true;
        public bool GenerateStaticMethods { get; set; } = true;
        public string? GeneratedNamespace { get; set; }
        public List<TraitProperty> Properties { get; set; } = new List<TraitProperty>();
        public List<string> InvalidMembers { get; set; } = new List<string>();
        public Location? Location { get; set; }

        /// <summary>
        /// Base traits this trait inherits from (direct [Trait]-annotated base interfaces).
        /// </summary>
        public List<TraitModel> BaseTraits { get; set; } = new List<TraitModel>();

        /// <summary>
        /// Merged property list including inherited properties (depth-first, diamond-deduplicated).
        /// Empty until <see cref="TraitGenerator.BuildAllProperties"/> is called.
        /// </summary>
        public List<TraitProperty> AllProperties { get; set; } = new List<TraitProperty>();

        /// <summary>
        /// Methods declared directly on this trait interface.
        /// </summary>
        public List<TraitMethod> Methods { get; set; } = new List<TraitMethod>();

        /// <summary>
        /// Merged method list including inherited methods (depth-first, diamond-deduplicated).
        /// Empty until BuildAllMethods is called.
        /// </summary>
        public List<TraitMethod> AllMethods { get; set; } = new List<TraitMethod>();

        /// <summary>
        /// True when this trait has at least one method (own or inherited).
        /// </summary>
        public bool HasMethods => Methods.Count > 0 || AllMethods.Count > 0;

        /// <summary>
        /// True when this trait has at least one base trait.
        /// </summary>
        public bool HasBaseTraits => BaseTraits.Count > 0;

        public string EffectiveNamespace => GeneratedNamespace ?? Namespace;

        /// <summary>
        /// Gets the short name without leading 'I' for extension method naming.
        /// E.g., ICoordinate -> Coordinate, IPoint2D -> Point2D
        /// </summary>
        public string ShortName
        {
            get
            {
                if (Name.Length > 1 && Name[0] == 'I' && char.IsUpper(Name[1]))
                    return Name.Substring(1);
                return Name;
            }
        }
    }

    internal sealed class TraitProperty
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
        public ITypeSymbol? Type { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
    }

    internal sealed class TraitMethod
    {
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "void";
        public bool ReturnsVoid => ReturnType == "void";
        public bool ReturnsSelf { get; set; }
        public List<TraitMethodParameter> Parameters { get; set; } = new List<TraitMethodParameter>();
        public string OverloadSuffix { get; set; } = "";

        /// <summary>
        /// The generated implementation method name: {Name}{OverloadSuffix}_Impl
        /// </summary>
        public string ImplMethodName => $"{Name}{OverloadSuffix}_Impl";

        /// <summary>
        /// True when the trait interface method has a default body (C# default interface method).
        /// When true, the generator can emit the default implementation for types that
        /// don't provide their own override via a static {Name}_Impl method.
        /// </summary>
        public bool HasDefaultBody { get; set; }

        /// <summary>
        /// The raw syntax text of the default method body, extracted from the interface declaration.
        /// This is the body as-written in the trait interface, before rewriting.
        /// Null when <see cref="HasDefaultBody"/> is false.
        /// </summary>
        public string? DefaultBodySyntax { get; set; }
    }

    internal sealed class TraitMethodParameter
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
        public bool IsSelf { get; set; }

        /// <summary>
        /// Parameter modifier: "in", "ref", "out", or "" (none).
        /// </summary>
        public string Modifier { get; set; } = "";
    }
}
