using System;
using System.Runtime.CompilerServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A read-only enumerable over two trait span views in lockstep.
    /// Created by <see cref="ReadOnlyTraitSpan{TLayout}.Zip{T2}(ReadOnlyTraitSpan{T2})"/>.
    /// Uses fused two-pointer walk: single MoveNext advances both pointers by shared stride.
    /// </summary>
    public readonly ref struct ReadOnlyTraitZipPairs<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly ref byte _ref1;
        private readonly ref byte _ref2;
        private readonly int _stride;
        private readonly int _length;

        /// <summary>
        /// Creates a ReadOnlyTraitZipPairs from two base references, a shared stride, and length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyTraitZipPairs(ref byte ref1, ref byte ref2, int stride, int length)
        {
            _ref1 = ref ref1;
            _ref2 = ref ref2;
            _stride = stride;
            _length = length;
        }

        /// <summary>Gets the number of element pairs.</summary>
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
        /// Enumerates pairs of trait layout references using fused two-pointer walk.
        /// Uses pointer increment (add) per step instead of multiply for performance.
        /// </summary>
        public ref struct Enumerator
        {
            private ref byte _current1;
            private ref byte _current2;
            private readonly int _stride;
            private readonly int _length;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(ReadOnlyTraitZipPairs<T1, T2> zip)
            {
                _current1 = ref Unsafe.SubtractByteOffset(ref Unsafe.AsRef(in zip._ref1), (nint)zip._stride);
                _current2 = ref Unsafe.SubtractByteOffset(ref Unsafe.AsRef(in zip._ref2), (nint)zip._stride);
                _stride = zip._stride;
                _length = zip._length;
                _index = -1;
            }

            /// <summary>Advances the enumerator to the next pair.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _length)
                {
                    _index = index;
                    _current1 = ref Unsafe.AddByteOffset(ref _current1, (nint)_stride);
                    _current2 = ref Unsafe.AddByteOffset(ref _current2, (nint)_stride);
                    return true;
                }
                return false;
            }

            /// <summary>Gets the current pair of trait layout references.</summary>
            public TraitPair<T1, T2> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new(ref _current1, ref _current2);
            }
        }
    }
}
