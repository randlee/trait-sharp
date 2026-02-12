using System;
using System.Diagnostics.CodeAnalysis;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// Helper for throwing exceptions from span bounds checks.
    /// Methods are not inlined to keep hot paths small.
    /// The [DoesNotReturn] attribute allows the JIT to omit dead code after throw sites,
    /// producing tighter codegen on the fast path.
    /// </summary>
    public static class ThrowHelper
    {
        /// <summary>Throws an <see cref="IndexOutOfRangeException"/>.</summary>
        [DoesNotReturn]
        public static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/>.</summary>
        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }

        /// <summary>Throws an <see cref="ArgumentException"/> indicating the destination is too short.</summary>
        [DoesNotReturn]
        public static void ThrowArgumentException_DestinationTooShort()
        {
            throw new ArgumentException("Destination is too short.");
        }

        /// <summary>Throws an <see cref="ArgumentException"/> indicating invalid dimensions.</summary>
        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidDimensions()
        {
            throw new ArgumentException("Source length must equal width * height.");
        }
    }
}
