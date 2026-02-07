using System;

namespace TraitEmulation
{
    /// <summary>
    /// Indicates this type implements the specified trait.
    /// Type must be partial struct/class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ImplementsTraitAttribute : Attribute
    {
        /// <summary>
        /// The trait interface this type implements.
        /// </summary>
        public Type TraitType { get; }

        /// <summary>
        /// How to implement the trait.
        /// </summary>
        public ImplStrategy Strategy { get; set; } = ImplStrategy.Auto;

        /// <summary>
        /// For Strategy.FieldMapping - specify custom field names.
        /// Format: "TraitProperty:ActualField,..."
        /// Example: "X:PositionX,Y:PositionY"
        /// </summary>
        public string? FieldMapping { get; set; }

        public ImplementsTraitAttribute(Type traitType)
        {
            TraitType = traitType ?? throw new ArgumentNullException(nameof(traitType));
        }
    }
}
