using System.Runtime.CompilerServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A read-only pair of trait layout references yielded by <see cref="ReadOnlyTraitZipPairs{T1, T2}"/>.
    /// </summary>
    public readonly ref struct TraitPair<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly ref byte _ref1;
        private readonly ref byte _ref2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TraitPair(ref byte ref1, ref byte ref2)
        {
            _ref1 = ref ref1;
            _ref2 = ref ref2;
        }

        /// <summary>Gets a read-only reference to the first trait layout.</summary>
        public ref readonly T1 First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T1>(ref Unsafe.AsRef(in _ref1));
        }

        /// <summary>Gets a read-only reference to the second trait layout.</summary>
        public ref readonly T2 Second
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T2>(ref Unsafe.AsRef(in _ref2));
        }
    }

    /// <summary>
    /// A mutable pair of trait layout references yielded by <see cref="TraitZipPairs{T1, T2}"/>.
    /// </summary>
    public ref struct MutableTraitPair<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly ref byte _ref1;
        private readonly ref byte _ref2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MutableTraitPair(ref byte ref1, ref byte ref2)
        {
            _ref1 = ref ref1;
            _ref2 = ref ref2;
        }

        /// <summary>Gets a mutable reference to the first trait layout.</summary>
        public ref T1 First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T1>(ref _ref1);
        }

        /// <summary>Gets a mutable reference to the second trait layout.</summary>
        public ref T2 Second
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T2>(ref _ref2);
        }
    }

    /// <summary>
    /// A read-only triple of trait layout references yielded by <see cref="ReadOnlyTraitZipTriples{T1, T2, T3}"/>.
    /// </summary>
    public readonly ref struct TraitTriple<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private readonly ref byte _ref1;
        private readonly ref byte _ref2;
        private readonly ref byte _ref3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TraitTriple(ref byte ref1, ref byte ref2, ref byte ref3)
        {
            _ref1 = ref ref1;
            _ref2 = ref ref2;
            _ref3 = ref ref3;
        }

        /// <summary>Gets a read-only reference to the first trait layout.</summary>
        public ref readonly T1 First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T1>(ref Unsafe.AsRef(in _ref1));
        }

        /// <summary>Gets a read-only reference to the second trait layout.</summary>
        public ref readonly T2 Second
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T2>(ref Unsafe.AsRef(in _ref2));
        }

        /// <summary>Gets a read-only reference to the third trait layout.</summary>
        public ref readonly T3 Third
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T3>(ref Unsafe.AsRef(in _ref3));
        }
    }

    /// <summary>
    /// A mutable triple of trait layout references yielded by <see cref="TraitZipTriples{T1, T2, T3}"/>.
    /// </summary>
    public ref struct MutableTraitTriple<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private readonly ref byte _ref1;
        private readonly ref byte _ref2;
        private readonly ref byte _ref3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MutableTraitTriple(ref byte ref1, ref byte ref2, ref byte ref3)
        {
            _ref1 = ref ref1;
            _ref2 = ref ref2;
            _ref3 = ref ref3;
        }

        /// <summary>Gets a mutable reference to the first trait layout.</summary>
        public ref T1 First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T1>(ref _ref1);
        }

        /// <summary>Gets a mutable reference to the second trait layout.</summary>
        public ref T2 Second
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T2>(ref _ref2);
        }

        /// <summary>Gets a mutable reference to the third trait layout.</summary>
        public ref T3 Third
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T3>(ref _ref3);
        }
    }
}
