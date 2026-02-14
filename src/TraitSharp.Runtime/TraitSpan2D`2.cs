using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitSharp.Runtime
{
    /// <summary>
    /// A high-performance mutable 2D view over a contiguous region of unmanaged structs,
    /// projected through a trait layout with zero byte offset.
    /// <para>
    /// Unlike <see cref="TraitSpan2D{TLayout}"/> which uses a runtime stride field,
    /// this type preserves the source type <typeparamref name="TSource"/> as a generic parameter.
    /// The JIT constant-folds <c>Unsafe.SizeOf&lt;TSource&gt;()</c>, producing optimal indexer code.
    /// </para>
    /// </summary>
    /// <typeparam name="TLayout">The trait layout struct type (the projected view).</typeparam>
    /// <typeparam name="TSource">The source struct type (the backing data).</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public ref struct TraitSpan2D<TLayout, TSource>
        where TLayout : unmanaged
        where TSource : unmanaged
    {
        private readonly ref TSource _reference;
        private readonly int _width;
        private readonly int _height;

        /// <summary>
        /// Creates a TraitSpan2D from a source reference and dimensions.
        /// The trait layout must start at byte offset 0 within TSource.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan2D(ref TSource reference, int width, int height)
        {
            _reference = ref reference;
            _width = width;
            _height = height;
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

        /// <summary>Gets the stride (byte distance between successive source elements). JIT constant-folded.</summary>
        public int Stride
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<TSource>();
        }

        /// <summary>Gets the pitch (byte distance between the start of successive rows). JIT constant-folded.</summary>
        public int Pitch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<TSource>() * _width;
        }

        /// <summary>Gets whether the data is contiguous (layout size equals source size).</summary>
        public bool IsContiguous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.SizeOf<TSource>() == Unsafe.SizeOf<TLayout>();
        }

        /// <summary>
        /// Returns a native Span&lt;TLayout&gt; over the same data when contiguous (row-major order).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the data is not contiguous.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<TLayout> AsNativeSpan()
        {
            if (Unsafe.SizeOf<TSource>() != Unsafe.SizeOf<TLayout>())
                ThrowHelper.ThrowInvalidOperationException_NotContiguous();
            return MemoryMarshal.CreateSpan(ref Unsafe.As<TSource, TLayout>(ref _reference), _width * _height);
        }

        /// <summary>
        /// Attempts to return a native Span&lt;TLayout&gt; over the same data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAsNativeSpan(out Span<TLayout> result)
        {
            if (Unsafe.SizeOf<TSource>() == Unsafe.SizeOf<TLayout>())
            {
                result = MemoryMarshal.CreateSpan(ref Unsafe.As<TSource, TLayout>(ref _reference), _width * _height);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Returns a mutable reference to the element at (row, col).
        /// Uses Unsafe.Add&lt;TSource&gt; so the JIT constant-folds the element stride.
        /// </summary>
        public ref TLayout this[int row, int col]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)row >= (uint)_height || (uint)col >= (uint)_width)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<TSource, TLayout>(
                    ref Unsafe.Add(ref _reference, row * _width + col));
            }
        }

        /// <summary>
        /// Returns a reference to the element at (0,0) without bounds checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TLayout DangerousGetReference()
        {
            return ref Unsafe.As<TSource, TLayout>(ref _reference);
        }

        /// <summary>
        /// Returns a reference to the element at (row, col) without bounds checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TLayout DangerousGetReferenceAt(int row, int col)
        {
            return ref Unsafe.As<TSource, TLayout>(
                ref Unsafe.Add(ref _reference, row * _width + col));
        }

        /// <summary>
        /// Gets a single row as a TraitSpan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout, TSource> GetRow(int row)
        {
            if ((uint)row >= (uint)_height)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan<TLayout, TSource>(
                ref Unsafe.Add(ref _reference, row * _width),
                _width);
        }

        /// <summary>
        /// Gets a sub-region of full-width rows from this 2D span.
        /// Returns the optimized two-parameter form.
        /// </summary>
        public TraitSpan2D<TLayout, TSource> SliceRows(int rowStart, int height)
        {
            if ((uint)rowStart > (uint)_height || (uint)height > (uint)(_height - rowStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan2D<TLayout, TSource>(
                ref Unsafe.Add(ref _reference, rowStart * _width),
                _width,
                height);
        }

        /// <summary>
        /// Gets a sub-region of this 2D span.
        /// Returns the strided single-parameter form to support arbitrary sub-column slicing.
        /// </summary>
        public TraitSpan2D<TLayout> Slice(int rowStart, int colStart, int height, int width)
        {
            if ((uint)rowStart > (uint)_height || (uint)height > (uint)(_height - rowStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)colStart > (uint)_width || (uint)width > (uint)(_width - colStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            int stride = Unsafe.SizeOf<TSource>();
            int rowStride = _width * stride;
            return new TraitSpan2D<TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.As<TSource, byte>(ref _reference),
                    (nint)(rowStart * rowStride + colStart * stride)),
                stride,
                width,
                height,
                rowStride);
        }

        /// <summary>
        /// Flattens to a 1D TraitSpan (row-major order).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout, TSource> AsSpan() =>
            new(ref _reference, _width * _height);

        /// <summary>
        /// Fills all elements with the specified value.
        /// </summary>
        public void Fill(TLayout value)
        {
            for (int r = 0; r < _height; r++)
            {
                var row = GetRow(r);
                row.Fill(value);
            }
        }

        /// <summary>Clears all trait-view fields to default.</summary>
        public void Clear() => Fill(default);

        /// <summary>Enumerates rows.</summary>
        public RowEnumerator EnumerateRows() => new(this);

        /// <summary>Enumerates rows of a TraitSpan2D.</summary>
        public ref struct RowEnumerator
        {
            private readonly TraitSpan2D<TLayout, TSource> _span;
            private int _row;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal RowEnumerator(TraitSpan2D<TLayout, TSource> span)
            {
                _span = span;
                _row = -1;
            }

            /// <summary>Advances the enumerator to the next row.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++_row < _span._height;

            /// <summary>Gets the row at the current position of the enumerator.</summary>
            public TraitSpan<TLayout, TSource> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _span.GetRow(_row);
            }

            /// <summary>Returns the enumerator itself for foreach support.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RowEnumerator GetEnumerator() => this;
        }

        /// <summary>
        /// Implicit conversion to the read-only two-parameter form.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyTraitSpan2D<TLayout, TSource>(TraitSpan2D<TLayout, TSource> span) =>
            new(ref span._reference, span._width, span._height);

        /// <summary>
        /// Implicit conversion to the strided single-parameter 2D form.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TraitSpan2D<TLayout>(TraitSpan2D<TLayout, TSource> span) =>
            new(ref Unsafe.As<TSource, byte>(ref span._reference),
                Unsafe.SizeOf<TSource>(),
                span._width,
                span._height);

        /// <summary>
        /// Implicit conversion to the strided single-parameter read-only 2D form.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyTraitSpan2D<TLayout>(TraitSpan2D<TLayout, TSource> span) =>
            new(ref Unsafe.As<TSource, byte>(ref span._reference),
                Unsafe.SizeOf<TSource>(),
                span._width,
                span._height);

        /// <summary>Returns an empty TraitSpan2D.</summary>
        public static TraitSpan2D<TLayout, TSource> Empty => default;
    }
}
