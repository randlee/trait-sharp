using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A high-performance read-only view over a contiguous region of unmanaged structs,
    /// projected through a trait layout with zero byte offset.
    /// <para>
    /// Unlike <see cref="ReadOnlyTraitSpan{TLayout}"/> which uses a runtime stride field,
    /// this type preserves the source type <typeparamref name="TSource"/> as a generic parameter.
    /// This allows the JIT to constant-fold <c>Unsafe.SizeOf&lt;TSource&gt;()</c> in the indexer,
    /// producing the same machine code quality as <see cref="ReadOnlySpan{T}"/>.
    /// </para>
    /// <para>
    /// Use this type when the trait offset is zero (the trait fields start at the beginning
    /// of the source struct). For non-zero offsets, use <see cref="ReadOnlyTraitSpan{TLayout}"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TLayout">The trait layout struct type (the projected view).</typeparam>
    /// <typeparam name="TSource">The source struct type (the backing data).</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public readonly ref struct ReadOnlyTraitSpan<TLayout, TSource>
        where TLayout : unmanaged
        where TSource : unmanaged
    {
        private readonly ref TSource _reference;
        private readonly int _length;

        /// <summary>
        /// Creates a ReadOnlyTraitSpan from a source reference and length.
        /// The trait layout must start at byte offset 0 within TSource.
        /// </summary>
        /// <param name="reference">Reference to the first source element.</param>
        /// <param name="length">Number of elements.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan(ref TSource reference, int length)
        {
            _reference = ref reference;
            _length = length;
        }

        /// <summary>Gets the number of elements in the span.</summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>Gets a value indicating whether this span is empty.</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == 0;
        }

        /// <summary>Gets the stride in bytes between successive elements (sizeof TSource, a JIT constant).</summary>
        public int Stride
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<TSource>();
        }

        /// <summary>Gets whether the data is contiguous (layout size equals source size).</summary>
        public bool IsContiguous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<TSource>() == Unsafe.SizeOf<TLayout>();
        }

        /// <summary>
        /// Returns a native ReadOnlySpan&lt;TLayout&gt; when the source and layout are the same size.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the data is not contiguous.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<TLayout> AsNativeSpan()
        {
            if (Unsafe.SizeOf<TSource>() != Unsafe.SizeOf<TLayout>())
                ThrowHelper.ThrowInvalidOperationException_NotContiguous();
            return MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<TSource, TLayout>(ref Unsafe.AsRef(in _reference)), _length);
        }

        /// <summary>
        /// Attempts to return a native ReadOnlySpan&lt;TLayout&gt;.
        /// Returns true if the source and layout are the same size; false otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAsNativeSpan(out ReadOnlySpan<TLayout> result)
        {
            if (Unsafe.SizeOf<TSource>() == Unsafe.SizeOf<TLayout>())
            {
                result = MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.As<TSource, TLayout>(ref Unsafe.AsRef(in _reference)), _length);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Returns a read-only reference to the element at the specified index.
        /// Uses Unsafe.Add&lt;TSource&gt; so the JIT constant-folds the stride.
        /// </summary>
        public ref readonly TLayout this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<TSource, TLayout>(
                    ref Unsafe.Add(ref Unsafe.AsRef(in _reference), index));
            }
        }

        /// <summary>
        /// Returns a read-only reference to the first element without bounds checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TLayout DangerousGetReference()
        {
            return ref Unsafe.As<TSource, TLayout>(ref Unsafe.AsRef(in _reference));
        }

        /// <summary>
        /// Returns a read-only reference to the element at the specified index without bounds checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TLayout DangerousGetReferenceAt(int index)
        {
            return ref Unsafe.As<TSource, TLayout>(
                ref Unsafe.Add(ref Unsafe.AsRef(in _reference), index));
        }

        /// <summary>
        /// Forms a slice starting at the specified index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan<TLayout, TSource> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new ReadOnlyTraitSpan<TLayout, TSource>(
                ref Unsafe.Add(ref Unsafe.AsRef(in _reference), start),
                _length - start);
        }

        /// <summary>
        /// Forms a slice starting at the specified index for the specified length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan<TLayout, TSource> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new ReadOnlyTraitSpan<TLayout, TSource>(
                ref Unsafe.Add(ref Unsafe.AsRef(in _reference), start),
                length);
        }

        /// <summary>
        /// Copies the contents of this span to a destination span.
        /// </summary>
        public void CopyTo(Span<TLayout> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            if (Unsafe.SizeOf<TSource>() == Unsafe.SizeOf<TLayout>())
            {
                // Contiguous: use bulk copy
                MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.As<TSource, TLayout>(ref Unsafe.AsRef(in _reference)), _length)
                    .CopyTo(destination);
                return;
            }
            // Strided copy using JIT-constant-folded Add<TSource>
            ref TSource src = ref Unsafe.AsRef(in _reference);
            for (int i = 0; i < _length; i++)
            {
                destination[i] = Unsafe.As<TSource, TLayout>(ref Unsafe.Add(ref src, i));
            }
        }

        /// <summary>
        /// Copies the contents of this span to a new array.
        /// </summary>
        public TLayout[] ToArray()
        {
            if (_length == 0) return Array.Empty<TLayout>();
            var array = new TLayout[_length];
            CopyTo(array);
            return array;
        }

        /// <summary>
        /// Returns the raw byte reference to the first element's trait view.
        /// For internal use by zip enumerators that need cross-type access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref byte DangerousGetRawReference()
        {
            return ref Unsafe.As<TSource, byte>(ref Unsafe.AsRef(in _reference));
        }

        /// <summary>
        /// Creates a fused zip enumerable that iterates this span and another in lockstep,
        /// yielding read-only pairs. Both spans must have the same length.
        /// </summary>
        /// <typeparam name="T2">The trait layout type of the second span.</typeparam>
        /// <param name="other">The second span to zip with.</param>
        /// <returns>A <see cref="ReadOnlyTraitZipPairs{TLayout, T2}"/> that can be enumerated with foreach.</returns>
        /// <exception cref="ArgumentException">Thrown when spans have different lengths.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitZipPairs<TLayout, T2> Zip<T2>(ReadOnlyTraitSpan<T2, TSource> other)
            where T2 : unmanaged
        {
            if (_length != other.Length)
                ThrowHelper.ThrowArgumentException_ZipLengthMismatch();
            int stride = Unsafe.SizeOf<TSource>();
            return new ReadOnlyTraitZipPairs<TLayout, T2>(
                ref DangerousGetRawReference(), ref other.DangerousGetRawReference(), stride, _length);
        }

        /// <summary>
        /// Creates a fused zip enumerable that iterates this span and another (single-param) in lockstep.
        /// Both spans must have the same stride and length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitZipPairs<TLayout, T2> Zip<T2>(ReadOnlyTraitSpan<T2> other)
            where T2 : unmanaged
        {
            int stride = Unsafe.SizeOf<TSource>();
            if (_length != other.Length)
                ThrowHelper.ThrowArgumentException_ZipLengthMismatch();
            if (stride != other.Stride)
                ThrowHelper.ThrowArgumentException_ZipStrideMismatch();
            return new ReadOnlyTraitZipPairs<TLayout, T2>(
                ref DangerousGetRawReference(), ref other.DangerousGetRawReference(), stride, _length);
        }

        /// <summary>
        /// Creates a fused zip enumerable that iterates this span and two others in lockstep,
        /// yielding read-only triples. All spans must have the same length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitZipTriples<TLayout, T2, T3> Zip<T2, T3>(ReadOnlyTraitSpan<T2, TSource> second, ReadOnlyTraitSpan<T3, TSource> third)
            where T2 : unmanaged
            where T3 : unmanaged
        {
            if (_length != second.Length || _length != third.Length)
                ThrowHelper.ThrowArgumentException_ZipLengthMismatch();
            int stride = Unsafe.SizeOf<TSource>();
            return new ReadOnlyTraitZipTriples<TLayout, T2, T3>(
                ref DangerousGetRawReference(), ref second.DangerousGetRawReference(), ref third.DangerousGetRawReference(), stride, _length);
        }

        /// <summary>Returns an enumerator for this span.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this);

        /// <summary>
        /// Enumerates elements of a ReadOnlyTraitSpan.
        /// Uses Unsafe.Add&lt;TSource&gt; with JIT-constant stride for optimal performance.
        /// </summary>
        public ref struct Enumerator
        {
            private readonly ref TSource _start;
            private readonly int _length;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(ReadOnlyTraitSpan<TLayout, TSource> span)
            {
                _start = ref Unsafe.AsRef(in span._reference);
                _length = span._length;
                _index = -1;
            }

            /// <summary>Advances the enumerator to the next element.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _length)
                {
                    _index = index;
                    return true;
                }
                return false;
            }

            /// <summary>Gets the element at the current position of the enumerator.</summary>
            public ref readonly TLayout Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.As<TSource, TLayout>(ref Unsafe.Add(ref _start, _index));
            }
        }

        /// <summary>
        /// Converts to the strided single-parameter form (for interop with APIs expecting ReadOnlyTraitSpan&lt;TLayout&gt;).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyTraitSpan<TLayout>(ReadOnlyTraitSpan<TLayout, TSource> span) =>
            new(ref Unsafe.As<TSource, byte>(ref Unsafe.AsRef(in span._reference)),
                Unsafe.SizeOf<TSource>(),
                span._length);

        /// <summary>Returns an empty ReadOnlyTraitSpan.</summary>
        public static ReadOnlyTraitSpan<TLayout, TSource> Empty => default;
    }
}
