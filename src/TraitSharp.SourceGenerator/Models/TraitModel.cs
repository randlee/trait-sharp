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
}
