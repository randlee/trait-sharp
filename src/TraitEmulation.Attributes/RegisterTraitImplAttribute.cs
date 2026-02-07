using System;

namespace TraitEmulation
{
    /// <summary>
    /// Registers a trait implementation for an external type (assembly-level).
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class RegisterTraitImplAttribute : Attribute
    {
        /// <summary>
        /// The trait interface to implement.
        /// </summary>
        public Type TraitType { get; }

        /// <summary>
        /// The external type to add trait implementation for.
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// How to implement the trait on the external type.
        /// </summary>
        public ImplStrategy Strategy { get; set; } = ImplStrategy.Auto;

        /// <summary>
        /// For Strategy.FieldMapping - map trait properties to target type members.
        /// </summary>
        public string? FieldMapping { get; set; }

        public RegisterTraitImplAttribute(Type traitType, Type targetType)
        {
            TraitType = traitType ?? throw new ArgumentNullException(nameof(traitType));
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        }
    }
}
