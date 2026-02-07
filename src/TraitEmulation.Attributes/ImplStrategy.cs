namespace TraitEmulation
{
    /// <summary>
    /// Strategy for implementing a trait.
    /// All strategies require layout-compatible fields.
    /// Layout incompatibility is always a compile error.
    /// </summary>
    public enum ImplStrategy
    {
        /// <summary>
        /// Generator verifies layout and emits reinterpret cast.
        /// Emit compile ERROR if layout incompatible.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Explicit declaration of reinterpret cast intent.
        /// Identical to Auto. Useful for self-documenting code.
        /// </summary>
        Reinterpret = 1,

        /// <summary>
        /// Trait properties map to different field names.
        /// Requires FieldMapping specification.
        /// Mapped fields must still be layout-compatible (contiguous, correct types).
        /// </summary>
        FieldMapping = 2
    }
}
