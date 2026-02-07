using System;

namespace TraitEmulation.Runtime
{
    /// <summary>
    /// Helper for throwing exceptions from span bounds checks.
    /// Methods are not inlined to keep hot paths small.
    /// </summary>
    public static class ThrowHelper
    {
        public static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }

        public static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }

        public static void ThrowArgumentException_DestinationTooShort()
        {
            throw new ArgumentException("Destination is too short.");
        }

        public static void ThrowArgumentException_InvalidDimensions()
        {
            throw new ArgumentException("Source length must equal width * height.");
        }
    }
}
