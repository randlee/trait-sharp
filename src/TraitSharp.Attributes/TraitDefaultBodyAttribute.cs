using System;

namespace TraitSharp
{
    /// <summary>
    /// Stores the default method body syntax for cross-assembly trait consumption.
    /// Applied by the source generator to a metadata class alongside the trait contract,
    /// allowing the consuming assembly's generator to discover default body text
    /// that would otherwise be unavailable across compilation boundaries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class TraitDefaultBodyAttribute : Attribute
    {
        /// <summary>
        /// The name of the trait method that has a default body.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// The raw body syntax text of the default method, in block form.
        /// E.g., "{ return X + Y; }" or "{ return 2f * (Width + Height); }"
        /// </summary>
        public string BodySyntax { get; }

        /// <summary>
        /// Creates a new TraitDefaultBodyAttribute.
        /// </summary>
        /// <param name="methodName">The method name.</param>
        /// <param name="bodySyntax">The default body syntax text.</param>
        public TraitDefaultBodyAttribute(string methodName, string bodySyntax)
        {
            MethodName = methodName;
            BodySyntax = bodySyntax;
        }
    }
}
