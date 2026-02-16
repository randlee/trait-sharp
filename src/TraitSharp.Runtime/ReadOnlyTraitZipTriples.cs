using System;
using System.Runtime.CompilerServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A read-only enumerable over three trait span views in lockstep.
    /// Created by <see cref="ReadOnlyTraitSpan{TLayout}.Zip{T2, T3}(ReadOnlyTraitSpan{T2}, ReadOnlyTraitSpan{T3})"/>.
    /// Uses fused three-pointer walk: single MoveNext advances all three pointers by shared stride.
    /// </summary>
    public readonly ref struct ReadOnlyTraitZipTriples<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private readonly ref byte _ref1;
        private readonly ref byte _ref2;
        private readonly ref byte _ref3;
        private readonly int _stride;
        private readonly int _length;

        /// <summary>
        /// Creates a ReadOnlyTraitZipTriples from three base references, a shared stride, and length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyTraitZipTriples(ref byte ref1, ref byte ref2, ref byte ref3, int stride, int length)
        {
            _ref1 = ref ref1;
            _ref2 = ref ref2;
            _ref3 = ref ref3;
            _stride = stride;
            _length = length;
        }

        /// <summary>Gets the number of element triples.</summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>Gets a value indicating whether this zip is empty.</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == 0;
        }

        /// <summary>Returns an enumerator for this zip.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this);

        /// <summary>
        /// Enumerates triples of trait layout references using fused three-pointer walk.
        /// Uses pointer increment (add) per step instead of multiply for performance.
        /// </summary>
        public ref struct Enumerator
        {
            private ref byte _current1;
            private ref byte _current2;
            private ref byte _current3;
            private readonly int _stride;
            private readonly int _length;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(ReadOnlyTraitZipTriples<T1, T2, T3> zip)
            {
                _current1 = ref Unsafe.SubtractByteOffset(ref Unsafe.AsRef(in zip._ref1), (nint)zip._stride);
                _current2 = ref Unsafe.SubtractByteOffset(ref Unsafe.AsRef(in zip._ref2), (nint)zip._stride);
                _current3 = ref Unsafe.SubtractByteOffset(ref Unsafe.AsRef(in zip._ref3), (nint)zip._stride);
                _stride = zip._stride;
                _length = zip._length;
                _index = -1;
            }

            /// <summary>Advances the enumerator to the next triple.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _length)
                {
                    _index = index;
                    _current1 = ref Unsafe.AddByteOffset(ref _current1, (nint)_stride);
                    _current2 = ref Unsafe.AddByteOffset(ref _current2, (nint)_stride);
                    _current3 = ref Unsafe.AddByteOffset(ref _current3, (nint)_stride);
                    return true;
                }
                return false;
            }

            /// <summary>Gets the current triple of trait layout references.</summary>
            public TraitTriple<T1, T2, T3> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new(ref _current1, ref _current2, ref _current3);
            }
        }
    }
}
