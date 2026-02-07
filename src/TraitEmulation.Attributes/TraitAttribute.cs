using System;

namespace TraitEmulation
{
    /// <summary>
    /// Marks an interface as a trait, triggering code generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class TraitAttribute : Attribute
    {
        /// <summary>
        /// Generate a layout struct for zero-copy field access.
        /// Default: true
        /// </summary>
        public bool GenerateLayout { get; set; } = true;

        /// <summary>
        /// Generate extension methods for ergonomic access.
        /// Default: true
        /// </summary>
        public bool GenerateExtensions { get; set; } = true;

        /// <summary>
        /// Generate static methods on the trait interface.
        /// Default: true
        /// </summary>
        public bool GenerateStaticMethods { get; set; } = true;

        /// <summary>
        /// Namespace for generated code. If null, uses trait's namespace.
        /// </summary>
        public string? GeneratedNamespace { get; set; }
    }
}
