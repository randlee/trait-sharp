using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A read-only 2D view over a contiguous region of unmanaged structs,
    /// projected through a trait layout. Provides row/column indexing over
    /// data stored in row-major order.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly ref struct ReadOnlyTraitSpan2D<TLayout>
        where TLayout : unmanaged
    {
        private readonly ref byte _reference;
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;      // bytes between successive source elements
        private readonly int _rowStride;   // bytes between successive rows (stride * width)

        /// <summary>
        /// Creates a ReadOnlyTraitSpan2D from a byte reference, stride, and dimensions.
        /// </summary>
        /// <param name="reference">Reference to the first trait-view byte (base + offset of element 0).</param>
        /// <param name="stride">Byte distance between successive source elements (sizeof source type).</param>
        /// <param name="width">Number of columns.</param>
        /// <param name="height">Number of rows.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan2D(ref byte reference, int stride, int width, int height)
        {
            _reference = ref reference;
            _stride = stride;
            _width = width;
            _height = height;
            _rowStride = stride * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyTraitSpan2D(ref byte reference, int stride, int width, int height, int rowStride)
        {
            _reference = ref reference;
            _stride = stride;
            _width = width;
            _height = height;
            _rowStride = rowStride;
        }

        /// <summary>Gets the width (number of columns).</summary>
        public int Width
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _width;
        }

        /// <summary>Gets the height (number of rows).</summary>
        public int Height
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _height;
        }

        /// <summary>Gets the total number of elements (Width * Height).</summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _width * _height;
        }

        /// <summary>Gets a value indicating whether this span is empty.</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _width == 0 || _height == 0;
        }

        /// <summary>Gets whether the data is contiguous (stride equals layout size), enabling native Span operations and SIMD.</summary>
        public bool IsContiguous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _stride == Unsafe.SizeOf<TLayout>();
        }

        /// <summary>
        /// Returns a native ReadOnlySpan&lt;TLayout&gt; over the same data when contiguous (row-major order).
        /// Enables SIMD/Vector&lt;T&gt; operations via MemoryMarshal.Cast on the result.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the data is not contiguous.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<TLayout> AsNativeSpan()
        {
            if (_stride != Unsafe.SizeOf<TLayout>())
                ThrowHelper.ThrowInvalidOperationException_NotContiguous();
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference)), _width * _height);
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
                result = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference)), _width * _height);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Returns a read-only reference to the element at (row, col).
        /// </summary>
        public ref readonly TLayout this[int row, int col]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)row >= (uint)_height || (uint)col >= (uint)_width)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<byte, TLayout>(
                    ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                        (nint)(row * _rowStride + col * _stride)));
            }
        }

        /// <summary>
        /// Returns a read-only reference to the element at (0,0) without bounds checking.
        /// The caller is responsible for ensuring the span is non-empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TLayout DangerousGetReference()
        {
            return ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference));
        }

        /// <summary>
        /// Returns a read-only reference to the element at (row, col) without bounds checking.
        /// The caller is responsible for ensuring the indices are within bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TLayout DangerousGetReferenceAt(int row, int col)
        {
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                    (nint)(row * _rowStride + col * _stride)));
        }

        /// <summary>
        /// Gets a single row as a ReadOnlyTraitSpan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan<TLayout> GetRow(int row)
        {
            if ((uint)row >= (uint)_height)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new ReadOnlyTraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference), (nint)(row * _rowStride)),
                _stride,
                _width);
        }

        /// <summary>
        /// Gets a sub-region of this 2D span.
        /// </summary>
        public ReadOnlyTraitSpan2D<TLayout> Slice(int rowStart, int colStart, int height, int width)
        {
            if ((uint)rowStart > (uint)_height || (uint)height > (uint)(_height - rowStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)colStart > (uint)_width || (uint)width > (uint)(_width - colStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new ReadOnlyTraitSpan2D<TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                    (nint)(rowStart * _rowStride + colStart * _stride)),
                _stride,
                width,
                height,
                _rowStride);
        }

        /// <summary>
        /// Flattens to a 1D ReadOnlyTraitSpan (row-major order).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyTraitSpan<TLayout> AsSpan() =>
            new(ref Unsafe.AsRef(in _reference), _stride, _width * _height);

        /// <summary>Enumerates rows.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RowEnumerator EnumerateRows() => new(this);

        /// <summary>Enumerates rows of a ReadOnlyTraitSpan2D.</summary>
        public ref struct RowEnumerator
        {
            private readonly ReadOnlyTraitSpan2D<TLayout> _span;
            private int _row;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal RowEnumerator(ReadOnlyTraitSpan2D<TLayout> span)
            {
                _span = span;
                _row = -1;
            }

            /// <summary>Advances the enumerator to the next row.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++_row < _span._height;

            /// <summary>Gets the row at the current position of the enumerator.</summary>
            public ReadOnlyTraitSpan<TLayout> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _span.GetRow(_row);
            }

            /// <summary>Returns the enumerator itself for foreach support.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RowEnumerator GetEnumerator() => this;
        }

        /// <summary>Returns an empty ReadOnlyTraitSpan2D.</summary>
        public static ReadOnlyTraitSpan2D<TLayout> Empty => default;
    }
}
