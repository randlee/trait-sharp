using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A read-only view over a contiguous region of unmanaged structs,
    /// projected through a trait layout at a fixed byte offset with stride.
    /// Analogous to ReadOnlySpan&lt;T&gt; but with offset/stride semantics.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly ref struct ReadOnlyTraitSpan<TLayout>
        where TLayout : unmanaged
    {
        private readonly ref byte _reference;
        private readonly int _length;
        private readonly int _stride;
        // _reference already points to baseOffset of first element

        /// <summary>
        /// Creates a ReadOnlyTraitSpan from a byte reference, stride, and length.
        /// </summary>
        /// <param name="reference">Reference to the first trait-view byte (base + offset of element 0).</param>
        /// <param name="stride">Byte distance between successive source elements (sizeof source type).</param>
        /// <param name="length">Number of elements.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan(ref byte reference, int stride, int length)
        {
            _reference = ref reference;
            _stride = stride;
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

        /// <summary>Gets the stride in bytes between successive elements.</summary>
        public int Stride
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _stride;
        }

        /// <summary>Gets whether the data is contiguous (stride equals layout size), enabling native Span operations and SIMD.</summary>
        public bool IsContiguous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _stride == Unsafe.SizeOf<TLayout>();
        }

        /// <summary>
        /// Returns a native ReadOnlySpan&lt;TLayout&gt; over the same data when contiguous.
        /// Enables SIMD/Vector&lt;T&gt; operations via MemoryMarshal.Cast on the result.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the data is not contiguous (stride != sizeof(TLayout)).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<TLayout> AsNativeSpan()
        {
            if (_stride != Unsafe.SizeOf<TLayout>())
                ThrowHelper.ThrowInvalidOperationException_NotContiguous();
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference)), _length);
        }

        /// <summary>
        /// Attempts to return a native ReadOnlySpan&lt;TLayout&gt; over the same data.
        /// Returns true and sets result if the data is contiguous; false otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAsNativeSpan(out ReadOnlySpan<TLayout> result)
        {
            if (_stride == Unsafe.SizeOf<TLayout>())
            {
                result = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference)), _length);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Returns a read-only reference to the element at the specified index.
        /// </summary>
        public ref readonly TLayout this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<byte, TLayout>(
                    ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                        (nint)(index * _stride)));
            }
        }

        /// <summary>
        /// Returns a read-only reference to the first element without bounds checking.
        /// The caller is responsible for ensuring the span is non-empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TLayout DangerousGetReference()
        {
            return ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference));
        }

        /// <summary>
        /// Returns a read-only reference to the element at the specified index without bounds checking.
        /// The caller is responsible for ensuring the index is within bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TLayout DangerousGetReferenceAt(int index)
        {
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                    (nint)(index * _stride)));
        }

        /// <summary>
        /// Returns the raw byte reference to the first element's trait view.
        /// For internal use by zip enumerators that need cross-type access.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref byte DangerousGetRawReference()
        {
            return ref Unsafe.AsRef(in _reference);
        }

        /// <summary>
        /// Forms a slice out of the current span starting at the specified index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan<TLayout> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new ReadOnlyTraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference), (nint)(start * _stride)),
                _stride,
                _length - start);
        }

        /// <summary>
        /// Forms a slice out of the current span starting at the specified index
        /// for the specified length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan<TLayout> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new ReadOnlyTraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference), (nint)(start * _stride)),
                _stride,
                length);
        }

        /// <summary>
        /// Copies the contents of this span to a destination span.
        /// Uses unchecked access internally for performance.
        /// </summary>
        public void CopyTo(Span<TLayout> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            if (_stride == Unsafe.SizeOf<TLayout>())
            {
                MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference)), _length)
                    .CopyTo(destination);
                return;
            }
            ref byte src = ref Unsafe.AsRef(in _reference);
            int stride = _stride;
            for (int i = 0; i < _length; i++)
            {
                destination[i] = Unsafe.As<byte, TLayout>(ref src);
                src = ref Unsafe.AddByteOffset(ref src, (nint)stride);
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
        /// Creates a fused zip enumerable that iterates this span and another in lockstep,
        /// yielding read-only pairs. Both spans must have the same stride and length.
        /// </summary>
        /// <typeparam name="T2">The trait layout type of the second span.</typeparam>
        /// <param name="other">The second span to zip with.</param>
        /// <returns>A <see cref="ReadOnlyTraitZipPairs{TLayout, T2}"/> that can be enumerated with foreach.</returns>
        /// <exception cref="ArgumentException">Thrown when spans have different lengths or strides.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitZipPairs<TLayout, T2> Zip<T2>(ReadOnlyTraitSpan<T2> other)
            where T2 : unmanaged
        {
            if (_length != other.Length)
                ThrowHelper.ThrowArgumentException_ZipLengthMismatch();
            if (_stride != other.Stride)
                ThrowHelper.ThrowArgumentException_ZipStrideMismatch();
            return new ReadOnlyTraitZipPairs<TLayout, T2>(
                ref Unsafe.AsRef(in _reference), ref other.DangerousGetRawReference(), _stride, _length);
        }

        /// <summary>
        /// Creates a fused zip enumerable that iterates this span and two others in lockstep,
        /// yielding read-only triples. All spans must have the same stride and length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitZipTriples<TLayout, T2, T3> Zip<T2, T3>(ReadOnlyTraitSpan<T2> second, ReadOnlyTraitSpan<T3> third)
            where T2 : unmanaged
            where T3 : unmanaged
        {
            if (_length != second.Length || _length != third.Length)
                ThrowHelper.ThrowArgumentException_ZipLengthMismatch();
            if (_stride != second.Stride || _stride != third.Stride)
                ThrowHelper.ThrowArgumentException_ZipStrideMismatch();
            return new ReadOnlyTraitZipTriples<TLayout, T2, T3>(
                ref Unsafe.AsRef(in _reference), ref second.DangerousGetRawReference(), ref third.DangerousGetRawReference(), _stride, _length);
        }

        /// <summary>Returns an enumerator for this span.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this);

        /// <summary>
        /// Enumerates elements of a ReadOnlyTraitSpan.
        /// Uses pointer increment (add) per step instead of multiply for performance.
        /// </summary>
        public ref struct Enumerator
        {
            private ref byte _current;
            private readonly int _stride;
            private readonly int _length;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(ReadOnlyTraitSpan<TLayout> span)
            {
                _current = ref Unsafe.SubtractByteOffset(ref Unsafe.AsRef(in span._reference), (nint)span._stride);
                _stride = span._stride;
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
                    _current = ref Unsafe.AddByteOffset(ref _current, (nint)_stride);
                    return true;
                }
                return false;
            }

            /// <summary>Gets the element at the current position of the enumerator.</summary>
            public ref readonly TLayout Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.As<byte, TLayout>(ref _current);
            }
        }

        /// <summary>Returns an empty ReadOnlyTraitSpan.</summary>
        public static ReadOnlyTraitSpan<TLayout> Empty => default;
    }
}
