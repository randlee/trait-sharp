using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A mutable view over a contiguous region of unmanaged structs,
    /// projected through a trait layout at a fixed byte offset with stride.
    /// Analogous to Span&lt;T&gt; but with offset/stride semantics.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public ref struct TraitSpan<TLayout>
        where TLayout : unmanaged
    {
        private readonly ref byte _reference;
        private readonly int _length;
        private readonly int _stride;

        /// <summary>
        /// Creates a TraitSpan from a byte reference, stride, and length.
        /// </summary>
        /// <param name="reference">Reference to the first trait-view byte (base + offset of element 0).</param>
        /// <param name="stride">Byte distance between successive source elements (sizeof source type).</param>
        /// <param name="length">Number of elements.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan(ref byte reference, int stride, int length)
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
        /// Returns a native Span&lt;TLayout&gt; over the same data when contiguous.
        /// Enables SIMD/Vector&lt;T&gt; operations via MemoryMarshal.Cast on the result.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the data is not contiguous (stride != sizeof(TLayout)).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<TLayout> AsNativeSpan()
        {
            if (_stride != Unsafe.SizeOf<TLayout>())
                ThrowHelper.ThrowInvalidOperationException_NotContiguous();
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, TLayout>(ref _reference), _length);
        }

        /// <summary>
        /// Attempts to return a native Span&lt;TLayout&gt; over the same data.
        /// Returns true and sets result if the data is contiguous; false otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAsNativeSpan(out Span<TLayout> result)
        {
            if (_stride == Unsafe.SizeOf<TLayout>())
            {
                result = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, TLayout>(ref _reference), _length);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Returns a mutable reference to the element at the specified index.
        /// </summary>
        public ref TLayout this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<byte, TLayout>(
                    ref Unsafe.AddByteOffset(ref _reference, (nint)(index * _stride)));
            }
        }

        /// <summary>
        /// Returns a reference to the first element without bounds checking.
        /// The caller is responsible for ensuring the span is non-empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TLayout DangerousGetReference()
        {
            return ref Unsafe.As<byte, TLayout>(ref _reference);
        }

        /// <summary>
        /// Returns a reference to the element at the specified index without bounds checking.
        /// The caller is responsible for ensuring the index is within bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TLayout DangerousGetReferenceAt(int index)
        {
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(index * _stride)));
        }

        /// <summary>
        /// Forms a slice out of the current span starting at the specified index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(start * _stride)),
                _stride,
                _length - start);
        }

        /// <summary>
        /// Forms a slice out of the current span starting at the specified index
        /// for the specified length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(start * _stride)),
                _stride,
                length);
        }

        /// <summary>
        /// Copies from this strided span into a contiguous destination.
        /// Uses unchecked access internally for performance.
        /// </summary>
        public void CopyTo(Span<TLayout> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            if (_stride == Unsafe.SizeOf<TLayout>())
            {
                MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.As<byte, TLayout>(ref _reference), _length)
                    .CopyTo(destination);
                return;
            }
            ref byte src = ref _reference;
            int stride = _stride;
            for (int i = 0; i < _length; i++)
            {
                destination[i] = Unsafe.As<byte, TLayout>(ref src);
                src = ref Unsafe.AddByteOffset(ref src, (nint)stride);
            }
        }

        /// <summary>
        /// Fills all elements with the specified value.
        /// Writes through the strided view into the source struct fields.
        /// Uses unchecked access internally for performance.
        /// </summary>
        public void Fill(TLayout value)
        {
            if (_stride == Unsafe.SizeOf<TLayout>())
            {
                MemoryMarshal.CreateSpan(
                    ref Unsafe.As<byte, TLayout>(ref _reference), _length)
                    .Fill(value);
                return;
            }
            ref byte dst = ref _reference;
            int stride = _stride;
            for (int i = 0; i < _length; i++)
            {
                Unsafe.As<byte, TLayout>(ref dst) = value;
                dst = ref Unsafe.AddByteOffset(ref dst, (nint)stride);
            }
        }

        /// <summary>Clears all trait-view fields to default.</summary>
        public void Clear() => Fill(default);

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

        /// <summary>Returns an enumerator for this span.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this);

        /// <summary>
        /// Enumerates elements of a TraitSpan.
        /// Uses pointer increment (add) per step instead of multiply for performance.
        /// </summary>
        public ref struct Enumerator
        {
            private ref byte _current;
            private readonly int _stride;
            private readonly int _length;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(TraitSpan<TLayout> span)
            {
                _current = ref Unsafe.SubtractByteOffset(ref span._reference, (nint)span._stride);
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
            public ref TLayout Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.As<byte, TLayout>(ref _current);
            }
        }

        /// <summary>
        /// Implicit conversion to ReadOnlyTraitSpan.
        /// </summary>
        public static implicit operator ReadOnlyTraitSpan<TLayout>(TraitSpan<TLayout> span) =>
            new(ref span._reference, span._stride, span._length);

        /// <summary>Returns an empty TraitSpan.</summary>
        public static TraitSpan<TLayout> Empty => default;
    }
}
